using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.PluginLogic;
class Tests {
 static int checks;
 static void Check(bool value, string message) {checks++; if(!value)throw new Exception(message);}
 static void Fails(Action action,string label) {bool failed=false;try{action();}catch{failed=true;}Check(failed,label);}
 static int Main(string[] args) {try {Run(args[0]);Console.WriteLine("PASS: "+checks+" checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Box(Stream stream,string type,byte[] data) {
 long size=data.Length+8;stream.Write(new[]{(byte)(size>>24),(byte)(size>>16),(byte)(size>>8),(byte)size},0,4);
 var name=System.Text.Encoding.ASCII.GetBytes(type);stream.Write(name,0,4);stream.Write(data,0,data.Length);
 }
 static void Run(string root) {
 CultureInfo.CurrentCulture=new CultureInfo("ru-RU");
 string srt="1\r\n00:00:01,234 --> 00:00:02,500\r\n<i>Привет</i>, \"мир\"\r\nВторая строка\r\n\r\n2\r\n01:02:03,456 --> 01:02:04,456\r\n{\\i1}English{\\i0}\r\n";
 var cues=MarkerExport.Parse(srt);Check(cues.Count==2,"SRT parse");
 var options=new ExportOptions();
 var csv=MarkerExport.Render(cues,options,null);
 Check(csv.StartsWith(MarkerExport.Header+"\r\n"),"Exact Audition header");
 Check(csv.Contains("\t01:234" )==false,"No invalid decimal time");
 Check(csv.Contains("\t00:01.234\t0:00.000\tdecimal\tCue\t"),"Point marker time");
 Check(csv.Contains("\t62:03.456\t"),"Hours become total minutes");
 Check(csv.Contains("\"Привет, \"\"мир\"\" Вторая строка\""),"Quote, Cyrillic and multiline escaping");
 Check(!csv.Contains("<i>") && !csv.Contains("{\\i"),"Remove markup");
 options.RangeMarkers=true;Check(MarkerExport.Render(cues,options,null).Contains("\t00:01.266\t"),"Range duration");
 options.SelectedOnly=true;
 var selected=MarkerExport.Render(cues,options,new[]{1});
 Check(!selected.Contains("Маркер 1") && selected.Contains("Маркер 2"),"Selected only, original numbering");
 options.RenumberSelected=true;
 var renumbered=MarkerExport.Render(cues,options,new[]{1});
 Check(renumbered.Contains("Маркер 1 (2)\t") && !renumbered.Contains("субтитр"),"Sequential name with bare original number");
 var many=Enumerable.Range(0,40).Select(i=>new Paragraph("row "+(i+1),i*1000,i*1000+500)).ToList();
 var sparse=MarkerExport.Render(many,options,new[]{37,24,24});
 Check(sparse.Contains("Маркер 1 (25)\t") && sparse.Contains("Маркер 2 (38)\t"),"Sparse selection follows document order, deduplicates and starts at 1");
 Check(sparse.Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries).Length==3,"Only requested selected cues");
 options.RangeMarkers=false;
 Check(MarkerExport.Render(many,options,new[]{24}).Contains("Маркер 1 (25)\t00:24.000\t0:00.000"),"Renumbered point keeps original time");
 options.SelectedOnly=false;
 Check(!MarkerExport.Render(cues,options,null).Contains("(2)"),"All-cue export ignores selected numbering option");
 options.SelectedOnly=true;options.TextAsName=true;
 Check(MarkerExport.Render(cues,options,new[]{1}).Contains("English\t"),"Text-as-name takes precedence");
 options.TextAsName=false;options.RenumberSelected=false;
 Check(MarkerExport.Render(cues,options,new[]{1}).Contains("Маркер 2\t"),"Switching off restores document numbering");
 var serializer=new System.Xml.Serialization.XmlSerializer(typeof(ExportOptions));
 using(var writer=new StringWriter()) {
  options.RenumberSelected=true;serializer.Serialize(writer,options);
  using(var reader=new StringReader(writer.ToString()))Check(((ExportOptions)serializer.Deserialize(reader)).RenumberSelected,"New option round-trips");
 }
 using(var reader=new StringReader("<ExportOptions><SelectedOnly>true</SelectedOnly></ExportOptions>"))
  Check(!((ExportOptions)serializer.Deserialize(reader)).RenumberSelected,"Existing settings retain old numbering");
 options.RenumberSelected=false;options.RangeMarkers=true;
 Fails(delegate{MarkerExport.Render(cues,options,new int[0]);},"No selected rows rejected");
 Fails(delegate{MarkerExport.Render(cues,options,new[]{9});},"Out-of-range selection rejected");
 options.SelectedOnly=false;options.TextAsName=true;options.IncludeDescription=false;
 Check(MarkerExport.Render(cues,options,null).Contains("English\t62:03.456\t00:01.000\tdecimal\tCue\t\r\n"),"Text name and empty description");
 Check(MarkerExport.Time(999.6)=="00:01.000","Millisecond rounding carry");
 Check(MarkerExport.Time(90061001)=="1501:01.001","Beyond 24 hours");
 Fails(delegate{MarkerExport.Time(-1);},"Negative time rejected");
 Fails(delegate{MarkerExport.Time(double.NaN);},"NaN rejected");
 Fails(delegate{MarkerExport.Parse("not a subtitle");},"Malformed input rejected");
 var bad=new List<Paragraph>{new Paragraph("bad",2000,1000)};
 Fails(delegate{MarkerExport.Render(bad,new ExportOptions(),null);},"Negative duration rejected");
 string ass="[Script Info]\r\nScriptType: v4.00+\r\n[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\nDialogue: 0,0:00:03.21,0:00:05.60,Default,Actor,0,0,0,,{\\b1}ASS, text 01:02:03.45\\NНовая строка\r\n";
 var subtitle=new Subtitle(); new AdvancedSubStationAlpha().LoadSubtitle(subtitle,ass.Split(new[]{"\r\n"},StringSplitOptions.None).ToList(),null);
 Check(subtitle.Paragraphs.Count==1,"ASS actual parser");
 var normalized=MarkerExport.Parse(subtitle.ToText(new SubRip()));
 var assCsv=MarkerExport.Render(normalized,new ExportOptions(),null);
 Check(assCsv.Contains("00:03.210") && assCsv.Contains("ASS, text 01:02:03.45 Новая строка"),"ASS tags, comma, embedded timestamp");
 string folder=Path.Combine(root,"files-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
 string first=MarkerExport.Save(folder,"Тест",csv);string second=MarkerExport.Save(folder,"Тест",selected);
 Check(first.EndsWith("Тест_audition.csv") && second.EndsWith("Тест_audition_1.csv"),"Collision safe filenames");
 Check(File.ReadAllText(first)==csv,"No overwrite");
 Check(!File.ReadAllBytes(first).Take(3).SequenceEqual(new byte[]{239,187,191}),"UTF8 without BOM matches original script");
 Check(!Directory.GetFiles(folder,"*.tmp").Any(),"Temporary files cleaned");
 Fails(delegate{MarkerExport.Save(Path.Combine(folder,"missing"),"x",csv);},"Missing folder rejected");
 var roleCues=new List<Paragraph>{new Paragraph("A",1000,3000){Actor="ДАНЯ"},new Paragraph("B",3000,5000){Actor="ЭМИ"},new Paragraph("C",5000,7000){Actor="ДАНЯ"},new Paragraph("Без имени",7000,8000){Actor=""}};
 var actorOptions=new ExportOptions{ActorsOnly=true,ActorNames=new[]{"ДАНЯ"},RenumberSelected=true,RangeMarkers=true};
 var roles=MarkerExport.Build(roleCues,actorOptions,null,0,double.PositiveInfinity,1);
 Check(roles.Count==2&&roles[0].Name=="Маркер 1 (1)"&&roles[1].Name=="Маркер 2 (3)","Actor filtering with original row numbers");
 actorOptions.ActorNames=new[]{"ДАНЯ","ЭМИ"};Check(MarkerExport.Build(roleCues,actorOptions,null,0,double.PositiveInfinity,1).Count==3,"Multiple actors");
 actorOptions.ActorNames=new[]{""};Check(MarkerExport.Build(roleCues,actorOptions,null,0,double.PositiveInfinity,1).Single().Description=="Без имени","Explicit no-actor choice");
 actorOptions.ActorNames=new string[0];Fails(delegate{MarkerExport.Build(roleCues,actorOptions,null,0,double.PositiveInfinity,1);},"No selected actor rejected");
 actorOptions.ActorNames=new[]{"missing"};Fails(delegate{MarkerExport.Build(roleCues,actorOptions,null,0,double.PositiveInfinity,1);},"Missing actor never exports everything");
 actorOptions.ActorNames=new[]{"ДАНЯ"};
 var clipped=MarkerExport.Build(roleCues,actorOptions,null,2000,6000,1);
 Check(clipped.Count==2&&clipped[0].Start==0&&clipped[0].Duration==1000&&clipped[1].Start==3000&&clipped[1].Duration==1000,"Clip rebases and clamps crossing ranges");
 Check(MarkerExport.Build(roleCues,actorOptions,null,0,10000,1.001)[0].Start>1000.9,"SMPTE clock scale applied");
 Check(roleCues[0].StartTime.TotalMilliseconds==1000&&roleCues[0].Actor=="ДАНЯ","Marker building preserves cues and actors");
 var withActors=MarkerExport.ParseCurrent(subtitle.ToText(new SubRip()),ass);
 Check(withActors.Count==1&&withActors[0].Actor=="Actor","Current ASS retains Actor field");
 var markers=new List<AuditionMarker>{new AuditionMarker{Name="Маркер 1 (25)",Description="<tag> & \"quoted\" 😀",Start=1234,Duration=500,Actor="ДАНЯ"}};
 string xml=Mp4Markers.Packet(null,markers);var doc=XDocument.Parse(xml);
 XNamespace dm="http://ns.adobe.com/xmp/1.0/DynamicMedia/";
 Check(doc.Descendants(dm+"startTime").Single().Value=="1234"&&doc.Descendants(dm+"duration").Single().Value=="500","XMP millisecond timestamps");
 Check(doc.Descendants(dm+"frameRate").Single().Value=="f1000"&&doc.Descendants(dm+"type").Single().Value=="Cue","XMP rate and Cue type");
 Check(doc.Descendants(dm+"comment").Single().Value==markers[0].Description,"XML escaping and emoji survive");
 XNamespace rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#";
 doc.Descendants(rdf+"Description").First().Add(new XElement("Unrelated","retain"));
 doc.Descendants(rdf+"Bag").First().Add(new XElement(rdf+"li",new XAttribute(rdf+"parseType","Resource"),new XElement(dm+"trackName","Other track")));
 var merged=XDocument.Parse(Mp4Markers.Packet(doc.ToString(),markers));
 Check(merged.Descendants(dm+"trackName").Count()==2&&merged.Descendants("Unrelated").Single().Value=="retain","Existing tracks and metadata preserved, owned track replaced");
 Fails(delegate{Mp4Markers.Packet("<!DOCTYPE x [<!ENTITY t SYSTEM 'file:///no'>]><x>&t;</x>",markers);},"XMP external entities rejected");
 string mp4=Path.Combine(folder,"synthetic.mp4");
 using(var f=File.Create(mp4)){Box(f,"ftyp",System.Text.Encoding.ASCII.GetBytes("isom\0\0\0\0isommp42"));Box(f,"moov",new byte[0]);Box(f,"mdat",new byte[]{1,2,3,4,5,6,7,8});}
 var original=File.ReadAllBytes(mp4);Mp4Markers.Embed(mp4,markers,null);var embedded=File.ReadAllBytes(mp4);
 Check(embedded.Take(original.Length).SequenceEqual(original),"Embedding leaves original MP4 boxes byte identical");
 Check(System.Text.Encoding.UTF8.GetString(embedded).Contains("Маркер 1 (25)"),"XMP inside MP4 file");
 var beforeCancel=File.ReadAllBytes(mp4);Fails(delegate{Mp4Markers.Embed(mp4,markers,()=>true);},"Cancellation raised");
 Check(File.ReadAllBytes(mp4).SequenceEqual(beforeCancel),"Cancel preserves complete original video");
 Mp4Markers.Embed(mp4,markers,null);
 Check(File.ReadAllBytes(mp4).Take(original.Length).SequenceEqual(original),"Repeat embedding keeps media offsets unchanged");
 string copy=Mp4Markers.ExportCopy(mp4,markers,null);Check(File.Exists(copy)&&copy!=mp4,"Manual embed creates separate copy");
 Check(!Directory.GetFiles(folder,".audition-*").Any(),"All metadata staging files cleaned");
 string broken=Path.Combine(folder,"bad.mp4");File.WriteAllBytes(broken,new byte[]{0,0,0,99,109,100,97,116});
 var brokenBytes=File.ReadAllBytes(broken);Fails(delegate{Mp4Markers.Embed(broken,markers,null);},"Invalid MP4 rejected");Check(File.ReadAllBytes(broken).SequenceEqual(brokenBytes),"Rejected video is unchanged");
 Fails(delegate{Mp4Markers.Embed(Path.ChangeExtension(mp4,"mkv"),markers,null);},"Unsupported container rejected");
 string zero=Path.Combine(folder,"zero.mp4");using(var f=File.Create(zero)){Box(f,"ftyp",System.Text.Encoding.ASCII.GetBytes("isom\0\0\0\0isom"));Box(f,"moov",new byte[0]);f.Write(new byte[]{0,0,0,0,109,100,97,116,1,2},0,10);}
 Mp4Markers.Embed(zero,markers,null);Check(File.ReadAllBytes(zero).Length>40,"Open-ended final box is safely bounded before append");
 Check(new AuditionMarkers().ToolbarText.Length>0,"Optional toolbar metadata");
 Check(typeof(AuditionMarkers).GetMethod("DoActionWithSelection")!=null,"Extended selection entry point");
 }
}
