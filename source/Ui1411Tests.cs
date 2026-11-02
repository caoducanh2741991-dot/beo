using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using V15Voice;

public static class Ui1411Tests {
 static int count, code;
 static readonly string[] Names={"Tạo voice","Hàng đợi nhiều file","Thư viện giọng","Công cụ & thiết lập","Sao chép giọng","MP4 / MP3 → SRT","Hướng dẫn"};
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 static IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
 static bool Ordered(TabControl tabs){return tabs.TabPages.Cast<TabPage>().Select(p=>p.Text.Trim()).SequenceEqual(Names);}
 static void Capture(Form f,string name){f.Refresh();Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(f.Location,Point.Empty,f.Size);bitmap.Save(Path.Combine(Path.GetTempPath(),name+".png"));}}
 [STAThread]public static void Main(){
  Application.EnableVisualStyles();var f=new MainForm();var tabs=(TabControl)Field(f,"tabs");
  Check(tabs.TabPages.Count==7,"All seven pages exist immediately after construction, before showing the form");
  Check(Ordered(tabs),"Startup tab order is Studio, queue, library, tools, cloning, guide");
  Check(tabs.TabPages.Cast<TabPage>().All(p=>p.Parent==tabs&&tabs.Controls.Contains(p)),"Every registered page is attached to the same TabControl");
  Check(!f.GetType().Assembly.GetTypes().Any(t=>t.Name.StartsWith("Creator")||t.Name=="ReviewTransport"||t.Name=="ReviewMediaHost"),"Removed video workflow and embedded player are absent from executable");
  f.Shown+=(s,e)=>{
   var timer=new Timer{Interval=350};int step=0;
   timer.Tick+=(a,b)=>{try{
    if(step==0){
     Check(f.Text.EndsWith("2.0.10")&&f.GetType().Assembly.GetName().Version==new Version(2,0,10,0),"Title and EXE assembly both identify 2.0.10");
     Check(tabs.TabCount==7&&Ordered(tabs),"All seven tabs remain registered after native handle creation");
     for(int i=0;i<7;i++){tabs.SelectedIndex=i;Check(tabs.SelectedTab==tabs.TabPages[i]&&tabs.TabPages[i].Visible,"Tab "+i+" can be selected and displays its page");}
     tabs.SelectedIndex=3;
    }else if(step==1){
     var device=(ComboBox)Field(f,"devicePicker");
     Check(device.Visible&&device.Parent!=null&&device.Items.Count==3,"GPU device picker is visible inside Tools with all three modes");
     Check(Walk(tabs.SelectedTab).OfType<Button>().Any(x=>x.Visible&&x.Text=="Cập nhật từ ZIP"),"ZIP update button is visible and reachable from Tools");
     Check(Walk(tabs.SelectedTab).OfType<Button>().Any(x=>x.Visible&&x.Text=="Kiểm tra GPU / VRAM"),"GPU diagnostics button is visible and reachable from Tools");
     device.SelectedIndex=1;var saved=(VoiceSettings)Call(f,"CaptureSettings");
     Check(saved.Device=="gpu"&&((KokoroEngine)Call(f,"NewEngine")).DeviceMode=="gpu","GPU selection reaches saved settings and the Studio engine");
     Check(((Label)Field(f,"batchInfo")).Text.Contains("NVIDIA GPU"),"Queue receives the same GPU selection");
     device.SelectedIndex=2;Call(f,"Apply",saved);Check(device.SelectedIndex==1,"Restoring settings restores the GPU selection");
     Check(Walk(tabs.SelectedTab).OfType<Button>().Any(x=>x.Visible&&x.Text=="Thử giọng đang chọn trên GPU"),"Tools exposes actual selected-voice GPU synthesis check");
     var pacing=(ComboBox)Field(f,"punctuationMode");pacing.SelectedIndex=1;((NumericUpDown)Field(f,"commaPause")).Value=.23m;((NumericUpDown)Field(f,"stopPause")).Value=.45m;
     var paced=(VoiceSettings)Call(f,"CaptureSettings");Check(paced.Punctuation==1&&paced.CommaPause==.23&&paced.StopPause==.45,"Studio custom pauses reach shared persistent settings");
     Check(((Label)Field(f,"batchInfo")).Text.Contains("nghỉ dấu câu: Tùy chỉnh"),"Queue summary reflects same punctuation mode");
     var ro=(RenderOptions)Call(f,"PacingOptions");Check(ro.Punctuation==PunctuationMode.Custom&&ro.CommaPause==.23,"Preview pacing uses same Studio values");
     pacing.SelectedIndex=0;Call(f,"Apply",paced);Check(pacing.SelectedIndex==1&&((NumericUpDown)Field(f,"commaPause")).Value==.23m,"Restoring preset restores custom pacing controls");
     tabs.SelectedIndex=0;((TabControl)Field(f,"studioSettings")).SelectedIndex=2;var settings=(FlowLayoutPanel)Field(f,"punctuationSettings");((ScrollableControl)settings.Parent).ScrollControlIntoView(settings);Application.DoEvents();Check(settings.Visible&&Walk(settings).OfType<NumericUpDown>().Count()==4,"Four punctuation values are reachable in Studio scroll panel");Capture(f,"v15_146_pacing");
     tabs.SelectedIndex=3;Capture(f,"v15_146_tools");tabs.SelectedIndex=4;
    }else if(step==2){
     Check(Walk(tabs.SelectedTab).OfType<Button>().Any(x=>x.Visible&&x.Text=="Chọn MP3 / WAV / MP4 mẫu"),"Cloning tab visibly exposes the MP3/WAV sample picker");
     Check(Walk(tabs.SelectedTab).OfType<ComboBox>().Any(x=>x.Visible&&x.Items.Count==3&&x.Items[0].ToString().Contains("Qwen3")&&x.Items[2].ToString().Contains("VieNeu")),"Cloning tab visibly exposes English and Vietnamese choices");
     Check(Walk(tabs.SelectedTab).OfType<ListBox>().Any(x=>x.Visible),"Saved custom voices list is attached and visible");
     Check(Walk(tabs.SelectedTab).OfType<Button>().Any(x=>x.Text=="▶ Nghe đoạn đang chọn"),"Reference excerpt can be previewed before saving");
     var quality=(FlowLayoutPanel)Field(f,"qualityControls");
     Check(((CheckBox)Field(f,"normalizeLoudness")).Checked&&((ComboBox)Field(f,"fitMode")).SelectedIndex==0,"Gentle rhythm and loudness enabled by default");
     var qs=(VoiceSettings)Call(f,"CaptureSettings");Check(qs.Normalize&&qs.TargetLufs==-18&&qs.GentleFit,"Quality options captured for presets and queue");
     ((ComboBox)Field(f,"fitMode")).SelectedIndex=1;Check(((NumericUpDown)Field(f,"minSpeed")).Enabled,"Custom mode enables speed bounds");
     Call(f,"Apply",qs);Check(!((NumericUpDown)Field(f,"maxSpeed")).Enabled,"Gentle mode disables custom upper bound");
     Capture(f,"v15_146_cloning");
     typeof(Control).GetMethod("RecreateHandle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tabs,null);
     Check(tabs.TabCount==7&&Ordered(tabs),"Recreating the TabControl handle preserves all seven tabs and their order");
     tabs.SelectedIndex=3;
    }else{
     Check(((ComboBox)Field(f,"devicePicker")).Visible&&((ComboBox)Field(f,"devicePicker")).SelectedIndex==1,"Tools remain reachable and retain selection after handle recreation");
     var speed=(NumericUpDown)Field(f,"speed");var cap=(NumericUpDown)Field(f,"maxSpeed");speed.Value=1.3m;
     Check(cap.Minimum>=speed.Value&&cap.Value>=speed.Value,"Timeline speed limit still follows the selected base speed");
     string txt=Path.Combine(Path.GetTempPath(),"ui-prose-"+Guid.NewGuid().ToString("N")+".txt");File.WriteAllText(txt,"Dr. Smith tells the story. This sentence continues naturally.\n\nA new paragraph starts here.");Call(f,"LoadFile",txt);tabs.SelectedIndex=0;
     Check((bool)Field(f,"proseInput")&&((ComboBox)Field(f,"mode")).SelectedIndex==1,"Plain TXT automatically activates natural voice timing");
     var prose=(FlowLayoutPanel)Field(f,"proseTools");((ScrollableControl)prose.Parent).ScrollControlIntoView(prose);Application.DoEvents();Check(prose.Visible&&Walk(prose).OfType<Button>().Count()==3,"Natural TXT controls for regenerate, previous version and locking are reachable");
     Check(((Label)Field(f,"proseTiming")).Text.Contains("dự kiến")&&!((ComboBox)Field(f,"mode")).Enabled,"Before rendering, duration is provisional and SRT lock is disabled");
     ((ComboBox)Field(f,"breathMode")).SelectedIndex=2;var txtOptions=(RenderOptions)Call(f,"Options");Check(txtOptions.Prose&&!txtOptions.Locked&&txtOptions.ReadingChanged!=null,"Studio passes natural mode and live timing callbacks to renderer");
     var txtEngine=(KokoroEngine)Call(f,"NewEngine");Check(txtEngine.Archive!=null,"TXT uses persistent audio store for continuation");
     var rows=(List<Cue>)Field(f,"cues");foreach(var cue in rows)txtEngine.Archive.Put(txtEngine,cue.Text,(double)speed.Value,new Wave{Rate=24000,Samples=new short[24000]});Call(f,"LockProse");Check(!String.IsNullOrEmpty(((List<Cue>)Field(f,"cues"))[0].VoiceLock)&&((DataGridView)Field(f,"grid")).Rows[0].Cells[3].ReadOnly,"Locking a generated paragraph protects its text");Call(f,"LockProse");Check(String.IsNullOrEmpty(((List<Cue>)Field(f,"cues"))[0].VoiceLock),"Paragraph can be unlocked");
     Call(f,"SetBusy",true);Check(!prose.Enabled,"Prose editing controls disabled during render");Call(f,"SetBusy",false);Check(prose.Enabled&&!((ComboBox)Field(f,"mode")).Enabled,"After render, natural controls enabled without enabling SRT lock");
     var fixedVoice=(Control)Field(f,"studioVoiceCard");var card=fixedVoice.RectangleToScreen(fixedVoice.ClientRectangle);var studio=tabs.TabPages[0].RectangleToScreen(tabs.TabPages[0].ClientRectangle);Check(card.Top>=studio.Top&&card.Bottom<=studio.Bottom,"Voice card is fully visible within Studio");
     var body=(ScrollableControl)prose.Parent;body.AutoScrollPosition=new Point(0,5000);Application.DoEvents();Check(fixedVoice.RectangleToScreen(fixedVoice.ClientRectangle).Location==card.Location,"Voice card remains fixed when TXT settings are scrolled");
     Call(f,"SaveProseProject");Check((bool)Call(f,"RestoreProseProject"),"Latest edited TXT can be restored after reopening");Capture(f,"v15_146_prose");
     timer.Stop();Console.WriteLine("TOTAL UI 2.0.10 "+count+" passed.");f.Close();
    }step++;
   }catch(Exception ex){timer.Stop();Console.WriteLine(ex);code=1;f.Close();}};
   timer.Start();
  };
  Application.Run(f);Environment.Exit(code);
 }
}
