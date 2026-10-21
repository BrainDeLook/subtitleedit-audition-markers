using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.PluginLogic;
class Tests {
 static int checks;
 static void Check(bool value, string message) {checks++; if(!value)throw new Exception(message);}
 static void Fails(Action action,string label) {bool failed=false;try{action();}catch{failed=true;}Check(failed,label);}
 static int Main(string[] args) {try {Run(args[0]);Console.WriteLine("PASS: "+checks+" checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
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
 Check(new AuditionMarkers().ToolbarText.Length>0,"Optional toolbar metadata");
 Check(typeof(AuditionMarkers).GetMethod("DoActionWithSelection")!=null,"Extended selection entry point");
 }
}
