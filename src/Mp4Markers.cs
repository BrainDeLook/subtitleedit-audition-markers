using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Nikse.SubtitleEdit.PluginLogic
{
    public class AuditionMarker
    {
        public string Name, Description, Actor;
        public double Start, Duration;
    }

    public static class Mp4Markers
    {
        // Adobe's XMP UUID, in on-disk (network) byte order, not Guid.ToByteArray order.
        // Adobe XMP Toolkit, ISOBaseMedia_Support.hpp: ISOMedia::k_xmpUUID.
        private static readonly byte[] Uuid = { 0xbe,0x7a,0xcf,0xcb,0x97,0xa9,0x42,0xe8,0x9c,0x71,0x99,0x94,0x91,0xe3,0xaf,0xac };
        // Version 1.2.0 wrote a mistaken UUID. Read it only to migrate our own metadata.
        private static readonly byte[] LegacyUuid = { 0xbe,0x7a,0xcf,0xcb,0x97,0xa9,0x42,0xe8,0x9c,0x71,0x99,0x9f,0xbe,0x5e,0xff,0xdb };
        public const string TrackName = "Subtitle Edit Classic / Audition Markers";
        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace Dm = "http://ns.adobe.com/xmp/1.0/DynamicMedia/";
        private static readonly XNamespace X = "adobe:ns:meta/";
        private class Box { public long Offset, Size; public int Header; public string Type; public bool OpenEnded; }

        private static long Integer(byte[] b, int offset, int length)
        {
            ulong value = 0;
            for (int n = 0; n < length; n++) value = (value << 8) | b[offset + n];
            if (value > long.MaxValue) throw new InvalidDataException("MP4 box size exceeds supported range.");
            return (long)value;
        }
        private static byte[] Read(Stream s, int length)
        {
            var b = new byte[length]; int read = 0;
            while (read < length) { int n = s.Read(b, read, length-read); if (n == 0) throw new EndOfStreamException("Truncated MP4 box."); read += n; }
            return b;
        }
        private static byte[] BigEndian(long size)
        { return new[] { (byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size }; }

        private static List<Box> Boxes(Stream file)
        {
            var result = new List<Box>(); long position = 0;
            while (position < file.Length)
            {
                if (result.Count > 1000000 || file.Length-position < 8) throw new InvalidDataException("Invalid MP4 box layout.");
                file.Position = position; var header = Read(file, 8); long size = Integer(header,0,4); int headerSize = 8;
                bool open = size == 0;
                if (size == 1) { size = Integer(Read(file,8),0,8); headerSize = 16; }
                else if (open) size = file.Length-position;
                if (size < headerSize || size > file.Length-position) throw new InvalidDataException("Invalid or truncated MP4 box size.");
                if (open && size > uint.MaxValue) throw new InvalidDataException("An open-ended box larger than 4 GiB cannot be safely extended.");
                result.Add(new Box { Offset=position, Size=size, Header=headerSize, Type=Encoding.ASCII.GetString(header,4,4), OpenEnded=open });
                position += size;
            }
            return result;
        }

        private static string Clean(string value)
        {
            var text = new StringBuilder(); value = value ?? "";
            for (int i=0; i<value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]) && i+1<value.Length && char.IsLowSurrogate(value[i+1])) { text.Append(value[i]).Append(value[++i]); }
                else if (XmlConvert.IsXmlChar(value[i])) text.Append(value[i]);
            }
            return text.ToString();
        }
        private static string Milliseconds(double value)
        {
            MarkerExport.Time(value); // Common finite/nonnegative/range validation.
            return Math.Round(value,MidpointRounding.AwayFromZero).ToString("0",CultureInfo.InvariantCulture);
        }

        public static string Packet(string existing, IList<AuditionMarker> markers)
        {
            if (markers == null || markers.Count == 0) throw new InvalidDataException("No matching subtitle markers.");
            XDocument doc;
            if (string.IsNullOrWhiteSpace(existing))
                doc = new XDocument(new XElement(X+"xmpmeta",new XAttribute(XNamespace.Xmlns+"x",X),
                    new XElement(Rdf+"RDF",new XAttribute(XNamespace.Xmlns+"rdf",Rdf),
                        new XElement(Rdf+"Description",new XAttribute(Rdf+"about",""),new XAttribute(XNamespace.Xmlns+"xmpDM",Dm)))));
            else using (var reader=XmlReader.Create(new StringReader(existing),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=16*1024*1024 }))
                doc=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
            var rdf=doc.Descendants(Rdf+"RDF").FirstOrDefault();
            if (rdf==null) throw new InvalidDataException("Existing XMP has no RDF document; it was not overwritten.");
            // Remove only our previous track; retain every other property and track.
            foreach (var li in rdf.Descendants(Dm+"Tracks").Elements(Rdf+"Bag").Elements(Rdf+"li").ToList())
            {
                var resource=li.Element(Rdf+"Description") ?? li;
                if ((string)resource.Element(Dm+"trackName")==TrackName || (string)resource.Attribute(Dm+"trackName")==TrackName) li.Remove();
            }
            var description=rdf.Elements(Rdf+"Description").FirstOrDefault();
            if(description==null){description=new XElement(Rdf+"Description",new XAttribute(Rdf+"about",""));rdf.Add(description);}
            var tracks=description.Element(Dm+"Tracks");
            if(tracks==null){tracks=new XElement(Dm+"Tracks",new XElement(Rdf+"Bag"));description.Add(tracks);}
            var bag=tracks.Element(Rdf+"Bag");
            if(bag==null)throw new InvalidDataException("Existing XMP track collection is not an RDF Bag.");
            var sequence=new XElement(Rdf+"Seq");
            foreach(var marker in markers)
                sequence.Add(new XElement(Rdf+"li",new XAttribute(Rdf+"parseType","Resource"),
                    new XElement(Dm+"startTime",Milliseconds(marker.Start)),new XElement(Dm+"duration",Milliseconds(marker.Duration)),
                    new XElement(Dm+"name",Clean(marker.Name)),new XElement(Dm+"comment",Clean(marker.Description)),
                    new XElement(Dm+"type","Cue"),new XElement(Dm+"speaker",Clean(marker.Actor))));
            bag.Add(new XElement(Rdf+"li",new XAttribute(Rdf+"parseType","Resource"),
                new XElement(Dm+"trackName",TrackName),new XElement(Dm+"trackType","Cue"),new XElement(Dm+"frameRate","f1000"),
                new XElement(Dm+"markers",sequence)));
            return doc.ToString(SaveOptions.DisableFormatting);
        }

        private static void Cancel(Func<bool> cancelled) { if(cancelled!=null && cancelled())throw new OperationCanceledException("Marker embedding cancelled; original video retained."); }

        // For the host's newly generated output only. Manual export uses a separate copy.
        public static void Embed(string path, IList<AuditionMarker> markers, Func<bool> cancelled)
        {
            if(!string.Equals(Path.GetExtension(path),".mp4",StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Embedded Audition markers currently require an MP4 output. Use CSV for other containers.");
            path=Path.GetFullPath(path);
            string temp=Path.Combine(Path.GetDirectoryName(path),".audition-"+Guid.NewGuid().ToString("N")+".tmp");
            var stamp=new FileInfo(path); long originalLength=stamp.Length; DateTime originalTime=stamp.LastWriteTimeUtc;
            try
            {
                using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                {
                    var boxes=Boxes(input);
                    if(!boxes.Any(b=>b.Type=="moov") || !boxes.Any(b=>b.Type=="mdat"))throw new InvalidDataException("MP4 must contain movie and media boxes.");
                    var ftyp=boxes.FirstOrDefault(b=>b.Type=="ftyp");
                    if(ftyp==null || ftyp.Size<ftyp.Header+8 || ftyp.Size>1024*1024)throw new InvalidDataException("Missing/invalid MP4 file type box.");
                    input.Position=ftyp.Offset+ftyp.Header; string brands=Encoding.ASCII.GetString(Read(input,(int)(ftyp.Size-ftyp.Header)));
                    if(brands.Substring(0,4)=="qt  " || Enumerable.Range(0,Math.Max(0,(brands.Length-8)/4)).Any(n=>brands.Substring(8+n*4,4)=="qt  "))
                        throw new NotSupportedException("QuickTime/MOV XMP storage is different; choose a genuine MP4 output.");
                    Box old=null; string existing=null;
                    foreach(var box in boxes.Where(b=>b.Type=="uuid" && b.Size>=b.Header+16))
                    {
                        input.Position=box.Offset+box.Header;
                        var id=Read(input,16);
                        if(!id.SequenceEqual(Uuid) && !id.SequenceEqual(LegacyUuid))continue;
                        if(box.Size-box.Header-16>16*1024*1024)throw new InvalidDataException("Existing XMP packet exceeds 16 MiB.");
                        string candidate=new UTF8Encoding(false,true).GetString(Read(input,(int)(box.Size-box.Header-16))).TrimEnd('\0');
                        if(id.SequenceEqual(LegacyUuid))
                        {
                            using(var reader=XmlReader.Create(new StringReader(candidate),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=16*1024*1024 }))
                                if(!XDocument.Load(reader).Descendants(Dm+"trackName").Any(n=>n.Value==TrackName))continue;
                        }
                        if(old!=null)throw new InvalidDataException("Multiple XMP UUID boxes; refusing an ambiguous update.");
                        old=box; existing=candidate;
                    }
                    byte[] packet=new UTF8Encoding(false).GetBytes(Packet(existing,markers));
                    input.Position=0; var buffer=new byte[1024*1024]; int read;
                    while((read=input.Read(buffer,0,buffer.Length))>0){Cancel(cancelled);output.Write(buffer,0,read);}
                    foreach(var box in boxes.Where(b=>b.OpenEnded)){output.Position=box.Offset;output.Write(BigEndian(box.Size),0,4);}
                    if(old!=null){output.Position=old.Offset+4;output.Write(Encoding.ASCII.GetBytes("free"),0,4);}
                    output.Position=output.Length; output.Write(BigEndian(packet.Length+24L),0,4);output.Write(Encoding.ASCII.GetBytes("uuid"),0,4);
                    output.Write(Uuid,0,16);output.Write(packet,0,packet.Length);output.Flush(true);
                }
                Cancel(cancelled);
                var current=new FileInfo(path);
                if(current.Length!=originalLength || current.LastWriteTimeUtc!=originalTime)throw new IOException("Video changed during marker writing; update discarded.");
                File.Replace(temp,path,null);
            }
            finally { if(File.Exists(temp))File.Delete(temp); }
        }

        public static string ExportCopy(string source, IList<AuditionMarker> markers, Func<bool> cancelled)
        {
            source=Path.GetFullPath(source);
            string folder=Path.GetDirectoryName(source), stem=Path.GetFileNameWithoutExtension(source)+"_audition";
            string temp=Path.Combine(folder,".audition-"+Guid.NewGuid().ToString("N")+".mp4");
            try
            {
                using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {var b=new byte[1024*1024];int n;while((n=input.Read(b,0,b.Length))>0){Cancel(cancelled);output.Write(b,0,n);}}
                Embed(temp,markers,cancelled);
                for(int n=0;n<10000;n++)
                {string path=Path.Combine(folder,stem+(n==0?"":"_"+n)+".mp4");try{File.Move(temp,path);return path;}catch(IOException){if(!File.Exists(path))throw;}}
                throw new IOException("Too many existing marker videos.");
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
