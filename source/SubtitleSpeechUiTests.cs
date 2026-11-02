using System;using System.IO;using System.Linq;using System.Drawing;using System.Reflection;using System.Collections.Generic;using System.Windows.Forms;using System.Xml.Linq;using V15Voice;
public static class SubtitleSpeechUiTests {
 static int count,code;
 static object F(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);}
 static void Check(bool b,string n){if(!b)throw new Exception(n);count++;Console.WriteLine("PASS "+n);}
 [STAThread]public static void Main(){Application.EnableVisualStyles();var f=new MainForm();f.Shown+=(s,e)=>{try{
 var mode=(ComboBox)F(f,"subtitleRepetitions");var page=(TabPage)F(f,"subtitlePage");((TabControl)F(f,"tabs")).SelectedTab=page;Application.DoEvents();
 Check(mode.Items.Count==2&&mode.SelectedIndex==0,"Preserve repetition is the default with an explicit clean option");
 var job=new SubtitleJob{Input=Path.Combine(Path.GetTempPath(),"speech fixture.mp4"),Track=0};
 foreach(string command in new[]{"run","refine","align"})Check(((string)Call(f,"SubtitleArgs",command,job,"result.xml","large-v3","cpu",false)).Contains("--repetitions preserve"),command+" carries the safe repetition policy");
 string first=(string)Call(f,"SubtitleProfile",job,"large-v3",false);mode.SelectedIndex=1;
 Check(((string)Call(f,"SubtitleArgs","run",job,"result.xml","large-v3","cpu",false)).Contains("--repetitions clean"),"Explicit normalization reaches worker arguments");
 Check(first!=(string)Call(f,"SubtitleProfile",job,"large-v3",false),"Changing repetition mode invalidates completed-job profile");
 Call(f,"SaveSubtitleQueue");mode.SelectedIndex=0;Call(f,"RestoreSubtitleQueue");Check(mode.SelectedIndex==0,"Selection events persist user changes immediately");
 mode.SelectedIndex=1;Call(f,"SaveSubtitleQueue");string queue=Path.Combine((string)F(f,"stateDir"),"subtitles","queue.xml");var x=XElement.Load(queue);Check((string)x.Attribute("repetitions")=="1","Repetition mode is stored in queue preferences");
 x.Attribute("repetitions").Remove();x.Save(queue);Call(f,"RestoreSubtitleQueue");Check(mode.SelectedIndex==0,"Old preferences migrate to preservation");
 Call(f,"SetBusy",true);Check(!mode.Enabled,"Repetition mode cannot change during a running batch");Call(f,"SetBusy",false);Check(mode.Enabled,"Mode unlocks after processing");
 var options=(ScrollableControl)mode.Parent;options.ScrollControlIntoView(mode);Application.DoEvents();Check(mode.Visible&&mode.RectangleToScreen(mode.ClientRectangle).IntersectsWith(page.RectangleToScreen(page.ClientRectangle)),"Mode is reachable on subtitles page");
 f.Size=new Size(1100,740);options.ScrollControlIntoView(mode);Application.DoEvents();Check(mode.Visible&&mode.RectangleToScreen(mode.ClientRectangle).IntersectsWith(options.RectangleToScreen(options.ClientRectangle)),"Mode remains reachable at minimum window size");
 var d=new SubtitleDocument{Duration=2,Repetitions="clean",Cues=new List<SubtitleCue>{new SubtitleCue{Start=0,End=1,Text="A sentence."}}};string p=Path.Combine(Path.GetTempPath(),"v1410-policy-"+Guid.NewGuid().ToString("N")+".xml");d.Save(p);Check(SubtitleDocument.Load(p).Repetitions=="clean","Project round-trip keeps its processing policy");
 using(var bitmap=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(f.Location,Point.Empty,f.Size);bitmap.Save(Path.Combine(Path.GetTempPath(),"v15_1410_subtitles.png"));}Console.WriteLine("TOTAL SPEECH UI "+count+" passed.");
 }catch(Exception ex){Console.WriteLine(ex);code=1;}finally{f.Close();}};Application.Run(f);Environment.Exit(code);}
}
