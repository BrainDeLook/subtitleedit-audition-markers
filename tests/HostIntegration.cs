using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.PluginLogic;
class TestPluginToolbar {
 const BindingFlags B=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static int count;
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr p,uint code);
 static void Check(bool v,string s){if(!v)throw new Exception(s);count++;Console.WriteLine("PASS "+s);}
 static object Get(object o,string s){return o.GetType().GetField(s,B).GetValue(o);}
 static object Call(object o,string s,params object[] a){return o.GetType().GetMethods(B).First(m=>m.Name==s&&m.GetParameters().Length==a.Length).Invoke(o,a);}
 static void Set(object o,string s,object v){o.GetType().GetField(s,B).SetValue(o,v);}
 static void Options(string root,bool selected) {using(var f=File.Create(Path.Combine(root,"Plugins","AuditionMarkers.settings.xml")))new XmlSerializer(typeof(ExportOptions)).Serialize(f,new ExportOptions{Configured=true,SelectedOnly=selected});}
 [STAThread] static int Main(string[] args) {uint code=0;try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);code=1;}Console.Out.Flush();Console.Error.Flush();TerminateProcess(GetCurrentProcess(),code);return (int)code;}
 static void Run(string[] args){
 string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"Plugins"));
 foreach(var field in new[]{"DataDirectory","SettingsFileName","WaveformsDirectory","AutoBackupDirectory","DictionariesDirectory","PluginsDirectory"})typeof(Configuration).GetField(field).SetValue(null,field=="SettingsFileName"?Path.Combine(root,"Settings.xml"):field=="PluginsDirectory"?Path.Combine(root,"Plugins"):root+Path.DirectorySeparatorChar);
 File.Copy(args[1],Path.Combine(root,"Plugins","AuditionMarkers.dll"),true);Options(root,false);
 Configuration.Settings.General.CheckForUpdates=false;Configuration.Settings.General.AutoSave=false;
 using(var main=new Nikse.SubtitleEdit.Forms.Main()){
 main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-30000,-30000);main.Show();Application.DoEvents();
 var tool=(ToolStrip)Get(main,"toolStrip1");
 Func<ToolStripSplitButton> button=()=>tool.Items.OfType<ToolStripSplitButton>().Single(i=>i.Name=="classicPluginToolbar_AuditionMarkers");
 Check(button()!=null,"DLL discovered and toolbar button created");
 Check(button().DropDownItems.Count==1,"Settings dropdown available");
 Call(main,"LoadPlugins");Call(main,"LoadPlugins");Check(tool.Items.Cast<ToolStripItem>().Count(i=>i.Name=="classicPluginToolbar_AuditionMarkers")==1,"Repeated loading does not duplicate buttons");
 var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("<i>Первая</i>",1000,2000));sub.Paragraphs.Add(new Paragraph("Вторая",3000,4250));sub.Renumber();sub.OriginalFormat=new SubRip();
 Set(main,"_subtitle",sub);Set(main,"_currentSubtitleFormat",new SubRip());Set(main,"_fileName",Path.Combine(root,"sample.srt"));
 var grid=(SubtitleListView)Get(main,"SubtitleListview1");Call(grid,"Fill",sub,null);grid.SelectIndexAndEnsureVisible(0);
 string before=sub.ToText(new SubRip());button().PerformButtonClick();
 string output=Path.Combine(root,"sample_audition.csv");Check(File.Exists(output),"Real toolbar click writes CSV");Check(File.ReadAllText(output).Contains("Первая")&&File.ReadAllText(output).Contains("Вторая"),"All cues exported");
 Check(sub.ToText(new SubRip())==before,"Document unchanged");Check(ReferenceEquals(Get(main,"_subtitle"),sub),"Export does not replace document");
 Options(root,true);grid.Items[0].Selected=false;grid.Items[1].Selected=true;button().PerformButtonClick();
 output=Path.Combine(root,"sample_audition_1.csv");Check(File.Exists(output),"Second click avoids overwrite");
 Check(!File.ReadAllText(output).Contains("Первая")&&File.ReadAllText(output).Contains("Вторая"),"Actual editor selection forwarded");
 using(var timer=new Timer{Interval=150}){
 bool captured=false;timer.Tick+=(s,e)=>{foreach(Form f in Application.OpenForms){if(f==main||!f.Text.Contains(" — "))continue;using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(Path.Combine(root,"settings.png"));}captured=true;timer.Stop();f.DialogResult=DialogResult.Cancel;break;}};
 timer.Start();new AuditionMarkers().Configure(main);Check(captured,"Settings dialog renders and cancels");}
 File.Delete(Path.Combine(root,"Plugins","AuditionMarkers.dll"));Call(main,"LoadPlugins");Check(!tool.Items.Cast<ToolStripItem>().Any(i=>i.Name.StartsWith("classicPluginToolbar_")),"Uninstall removes toolbar button");
 }
 Console.WriteLine("PASS TOTAL "+count);
 }
}
