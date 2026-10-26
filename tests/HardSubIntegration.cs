using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.PluginLogic;
class HardSubIntegration {
 const BindingFlags B=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static int count;
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr p,uint code);
 static void Check(bool v,string s){if(!v)throw new Exception(s);count++;Console.WriteLine("PASS "+s);}
 static object Get(object o,string s){return o.GetType().GetField(s,B).GetValue(o);}
 static object Call(object o,string s,params object[] a){return o.GetType().GetMethods(B).First(m=>m.Name==s&&m.GetParameters().Length==a.Length).Invoke(o,a);}
 static void Save(string root,ExportOptions opt){using(var f=File.Create(Path.Combine(root,"Plugins","AuditionMarkers.settings.xml")))new XmlSerializer(typeof(ExportOptions)).Serialize(f,opt);}
 [STAThread]static int Main(string[] a){uint result=0;try{Application.EnableVisualStyles();Run(a);}catch(Exception e){Console.Error.WriteLine(e);result=1;}Console.Out.Flush();Console.Error.Flush();TerminateProcess(GetCurrentProcess(),result);return (int)result;}
 static void Run(string[] a){
 string root=Path.GetFullPath(a[0]), dll=Path.GetFullPath(a[1]), input=Path.GetFullPath(a[2]),ffmpeg=Path.GetFullPath(a[3]);
 Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"Plugins"));
 foreach(var field in new[]{"DataDirectory","SettingsFileName","WaveformsDirectory","AutoBackupDirectory","DictionariesDirectory","PluginsDirectory"})typeof(Configuration).GetField(field).SetValue(null,field=="SettingsFileName"?Path.Combine(root,"Settings.xml"):field=="PluginsDirectory"?Path.Combine(root,"Plugins"):root+Path.DirectorySeparatorChar);
 Configuration.Settings.General.FFmpegLocation=ffmpeg;Configuration.Settings.General.CheckForUpdates=false;Configuration.Settings.General.AutoSave=false;
 var sub=new Subtitle();sub.Header=AdvancedSubStationAlpha.DefaultHeader;sub.Paragraphs.Add(new Paragraph("ДАНЯ A",1000,3000){Actor="ДАНЯ"});sub.Paragraphs.Add(new Paragraph("ЭМИ B",3000,5000){Actor="ЭМИ"});sub.Paragraphs.Add(new Paragraph("ДАНЯ C",5000,7000){Actor="ДАНЯ"});sub.Renumber();
 var info=new VideoInfo{Width=320,Height=180,FramesPerSecond=25,TotalMilliseconds=8000,TotalSeconds=8,TotalFrames=200,Success=true,VideoCodec="h264",FileType="mp4"};
 // No plugin should leave the audio settings layout untouched.
 using(var form=new GenerateVideoWithHardSubs(sub,new AdvancedSubStationAlpha(),input,info,null,false)){
 var group=(GroupBox)Get(form,"groupBoxAudio");int height=group.Height;form.InitializeHardSubPlugins(sub,new[]{0,2});Check(group.Height==height&&form.Controls.Find("EmbedAuditionMarkers",true).Length==0,"No plugin means no UI or processing addition");}
 File.Copy(dll,Path.Combine(root,"Plugins","AuditionMarkers.dll"),true);
 Save(root,new ExportOptions{Configured=true,ActorsOnly=true,ActorNames=new[]{"ДАНЯ"},RenumberSelected=true,RangeMarkers=true,EmbedOnHardSub=true});
 using(var form=new GenerateVideoWithHardSubs(sub,new AdvancedSubStationAlpha(),input,info,null,false)){
 form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();form.InitializeHardSubPlugins(sub,new[]{0,2});var group=(GroupBox)Get(form,"groupBoxAudio");
 var embed=(CheckBox)form.Controls.Find("EmbedAuditionMarkers",true).Single();Check(embed.Checked,"Installed plugin supplies hard-sub marker checkbox");
 var panel=embed.Parent;Check(panel.Top>=((Control)Get(form,"comboBoxAudioBitRate")).Bottom,"Plugin row does not overlap audio fields");
 using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(root,"hard-sub.png"));}
 var container=(ComboBox)Get(form,"_classicContainer");container.SelectedItem=".mp4";
 Get(form,"comboBoxVideoEncoding").GetType().GetProperty("SelectedItem").SetValue(Get(form,"comboBoxVideoEncoding"),"libx264",null);
 Get(form,"comboBoxPreset").GetType().GetProperty("SelectedItem").SetValue(Get(form,"comboBoxPreset"),"ultrafast",null);
 ((CheckBox)Get(form,"checkBoxCut")).Checked=true;
 Get(form,"numericUpDownCutFromSeconds").GetType().GetProperty("Value").SetValue(Get(form,"numericUpDownCutFromSeconds"),2m,null);Get(form,"numericUpDownCutToSeconds").GetType().GetProperty("Value").SetValue(Get(form,"numericUpDownCutToSeconds"),6m,null);
 string output=Path.Combine(root,"hard-sub.mp4");typeof(GenerateVideoWithHardSubs).GetProperty("VideoFileName").SetValue(form,output,null);
 Check((bool)Call(form,"ConvertVideo",true,input,sub),"Real FFmpeg hard-sub export and marker postprocessing succeed");
 var data=System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(output));
 Check(data.Contains("<xmpDM:name>Маркер 1 (1)</xmpDM:name>")&&data.Contains("<xmpDM:name>Маркер 2 (3)</xmpDM:name>")&&!data.Contains("<xmpDM:speaker>ЭМИ</xmpDM:speaker>"),"Only chosen actor embedded in real output");
 Check(data.Contains("<xmpDM:startTime>0</xmpDM:startTime>")&&data.Contains("<xmpDM:startTime>3000</xmpDM:startTime>"),"Hard-sub cut times rebase to output origin");
 Check(sub.Paragraphs.Count==3&&sub.Paragraphs[0].StartTime.TotalMilliseconds==1000&&sub.Paragraphs[1].Actor=="ЭМИ","Marker filter does not alter burned subtitles or source");
 }
 Console.WriteLine("PASS TOTAL "+count);
 }
}
