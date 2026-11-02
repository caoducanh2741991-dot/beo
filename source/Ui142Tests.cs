using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using V15Voice;

public static class Ui142Tests {
 static int count, code;
 static readonly string[] Names={"Tạo voice","Hàng đợi nhiều file","Thư viện & phong cách giọng","Công cụ & thiết lập 1.4.2","Sao chép giọng MP3 / WAV","Hướng dẫn"};
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 static IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
 static bool Ordered(TabControl tabs){return tabs.TabPages.Cast<TabPage>().Select(p=>p.Text.Trim()).SequenceEqual(Names);}
 static void Capture(Form f,string name){f.Refresh();Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(f.Location,Point.Empty,f.Size);bitmap.Save(Path.Combine(Path.GetTempPath(),name+".png"));}}
 [STAThread]public static void Main(){
  Application.EnableVisualStyles();var f=new MainForm();var tabs=(TabControl)Field(f,"tabs");
  Check(tabs.TabPages.Count==6,"All six pages exist immediately after construction, before showing the form");
  Check(Ordered(tabs),"Startup tab order is Studio, queue, library, tools, cloning, guide");
  Check(tabs.TabPages.Cast<TabPage>().All(p=>p.Parent==tabs&&tabs.Controls.Contains(p)),"Every registered page is attached to the same TabControl");
  f.Shown+=(s,e)=>{
   var timer=new Timer{Interval=350};int step=0;
   timer.Tick+=(a,b)=>{try{
    if(step==0){
     Check(f.Text.EndsWith("1.4.2")&&f.GetType().Assembly.GetName().Version==new Version(1,4,2,0),"Title and EXE assembly both identify 1.4.2");
     Check(tabs.TabCount==6&&Ordered(tabs),"All six tabs remain registered after native handle creation");
     for(int i=0;i<6;i++){tabs.SelectedIndex=i;Check(tabs.SelectedTab==tabs.TabPages[i]&&tabs.TabPages[i].Visible,"Tab "+i+" can be selected and displays its page");}
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
     Capture(f,"v15_142_tools");tabs.SelectedIndex=4;
    }else if(step==2){
     Check(Walk(tabs.SelectedTab).OfType<Button>().Any(x=>x.Visible&&x.Text=="Chọn MP3 / WAV mẫu"),"Cloning tab visibly exposes the MP3/WAV sample picker");
     Check(Walk(tabs.SelectedTab).OfType<ComboBox>().Any(x=>x.Visible&&x.Items.Count==2&&x.Items[0].ToString().Contains("Qwen3")),"Cloning tab visibly exposes Qwen and Chatterbox choices");
     Check(Walk(tabs.SelectedTab).OfType<ListBox>().Any(x=>x.Visible),"Saved custom voices list is attached and visible");
     Capture(f,"v15_142_cloning");
     typeof(Control).GetMethod("RecreateHandle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tabs,null);
     Check(tabs.TabCount==6&&Ordered(tabs),"Recreating the TabControl handle preserves all six tabs and their order");
     tabs.SelectedIndex=3;
    }else{
     Check(((ComboBox)Field(f,"devicePicker")).Visible&&((ComboBox)Field(f,"devicePicker")).SelectedIndex==1,"Tools remain reachable and retain selection after handle recreation");
     var speed=(NumericUpDown)Field(f,"speed");var cap=(NumericUpDown)Field(f,"maxSpeed");speed.Value=1.3m;
     Check(cap.Minimum>=speed.Value&&cap.Value>=speed.Value,"Timeline speed limit still follows the selected base speed");
     timer.Stop();Console.WriteLine("TOTAL UI 1.4.2 "+count+" passed.");f.Close();
    }step++;
   }catch(Exception ex){timer.Stop();Console.WriteLine(ex);code=1;f.Close();}};
   timer.Start();
  };
  Application.Run(f);Environment.Exit(code);
 }
}
