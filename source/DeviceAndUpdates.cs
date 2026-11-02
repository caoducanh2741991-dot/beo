using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
namespace V15Voice {
public partial class MainForm {
 ComboBox devicePicker=new ComboBox();Label deviceStatus=new Label();
 string SelectedDevice(){return devicePicker.SelectedIndex==1?"gpu":devicePicker.SelectedIndex==2?"cpu":"auto";}
 string SelectedDeviceLabel(){return devicePicker.SelectedIndex==1?"NVIDIA GPU":devicePicker.SelectedIndex==2?"CPU":"Auto GPU / CPU";}
 void ShowDevice(string message){if(IsDisposed||Disposing)return;Action update=()=>{deviceStatus.Text=message;deviceStatus.AutoSize=true;};if(InvokeRequired){if(IsHandleCreated)BeginInvoke(update);}else update();}
 void ShowActivity(string message){if(IsDisposed||Disposing)return;Action update=()=>status.Text=message;if(InvokeRequired){if(IsHandleCreated)BeginInvoke(update);}else update();}
 void BuildDeviceAndUpdates(FlowLayoutPanel panel){
  AddLabel(panel,"THIẾT BỊ CHẠY VOICE",true);devicePicker.Width=300;devicePicker.DropDownStyle=ComboBoxStyle.DropDownList;devicePicker.Items.AddRange(new object[]{"Auto · ưu tiên NVIDIA GPU","NVIDIA GPU · bắt buộc CUDA","CPU"});devicePicker.SelectedIndex=0;devicePicker.SelectedIndexChanged+=(s,e)=>{UpdateShared();ShowDevice("Đã chọn "+SelectedDeviceLabel()+" · áp dụng cho mọi nguồn giọng ở cả hai tab.");};panel.Controls.Add(devicePicker);
  deviceStatus.Text="Kokoro / LibriTTS / Kitten / Piper: gói GPU ONNX. Qwen / Chatterbox: gói GPU CUDA 12.4. Adam RVC cần cả hai gói; index chạy CPU. Mỗi gói chỉ cài một lần.";deviceStatus.AutoSize=true;deviceStatus.MaximumSize=new Size(320,0);deviceStatus.ForeColor=muted;panel.Controls.Add(deviceStatus);
  panel.Controls.Add(Btn("Kiểm tra GPU / VRAM",async(s,e)=>await ProbeDevice()));
  panel.Controls.Add(Btn("Thử giọng đang chọn trên GPU",async(s,e)=>await ProbeVoiceGpu()));
  AddLabel(panel,"CẬP NHẬT ỨNG DỤNG",true);panel.Controls.Add(Btn("Cập nhật từ ZIP",(s,e)=>ChooseUpdate()));
  AddLabel(panel,"Chọn ZIP ứng dụng hoặc phần 01 của gói GPU. Đặt đủ các ZIP GPU cạnh nhau. App lưu phiên, đóng và mở lại sau khi cập nhật.",false);
 }
 async Task ProbeDevice(){if(busy)return;SetBusy(true);ShowDevice("Đang kiểm tra CUDA trên máy…");try{
  string selected=SelectedDevice();string result=await Task.Run(()=>{
   var si=new ProcessStartInfo(ExternalSpeech.Python(root),KokoroEngine.Q(Path.Combine(root,"ai","worker.py"))+" --probe --device "+selected){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
   using(var p=new Process{StartInfo=si}){string output="",errors="";p.OutputDataReceived+=(x,v)=>{if(v.Data!=null)output+=v.Data+"\n";};p.ErrorDataReceived+=(x,v)=>{if(v.Data!=null&&errors.Length<4000)errors+=v.Data+"\n";};p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();if(!p.WaitForExit(60000)){p.Kill();throw new Exception("Kiểm tra GPU quá 60 giây.");}p.WaitForExit();foreach(string line in (output+"\n"+errors).Split('\n')){if(line.StartsWith("V15_PROBE\t"))return ExternalSpeech.Decode(line.Substring(10));if(line.StartsWith("V15_ERR\t"))throw new Exception(ExternalSpeech.Decode(line.Substring(8)));}throw new Exception("Không kiểm tra được GPU. "+errors);}
  });ShowDevice("Qwen / Chatterbox: "+result+"\nGPU ONNX: "+(NativeGpu.Installed(root)?"đã cài · dùng nút thử giọng để kiểm tra thực tế":"chưa cài gói ONNX"));
 }catch(Exception e){ShowDevice(e.Message+"\nGPU ONNX: "+(NativeGpu.Installed(root)?"đã cài · có thể thử giọng ONNX riêng":"chưa cài"));}finally{SetBusy(false);}}
 async Task ProbeVoiceGpu(){if(busy)return;string folder=Path.Combine(Path.GetTempPath(),"V15_gpu_test_"+Guid.NewGuid().ToString("N"));try{active=NewEngine();active.DeviceMode="gpu";active.CheckFiles();SetBusy(true);Directory.CreateDirectory(folder);ShowDevice("Đang tạo câu thử bằng GPU cho giọng đang chọn…");var wave=await Task.Run(()=>active.Generate(VietnameseSelected?"Đây là câu thử giọng tiếng Việt trên bộ xử lý đồ họa.":"This is a short test of the selected voice on the GPU.",1,Path.Combine(folder,"probe.wav")));ShowDevice(active.DeviceInfo+" · mẫu "+wave.Duration.ToString("0.00")+" giây");}catch(OperationCanceledException){ShowDevice("Đã dừng thử GPU.");}catch(Exception e){ShowDevice(e.Message);}finally{if(active!=null)active.Dispose();active=null;try{if(Directory.Exists(folder))Directory.Delete(folder,true);}catch{}SetBusy(false);}}
 void ChooseUpdate(){if(busy)return;using(var d=new OpenFileDialog{Filter="Gói cập nhật V15|*.zip",Title="Chọn ZIP cập nhật hoặc phần 01 của gói GPU"})if(d.ShowDialog()==DialogResult.OK)try{
  if(!File.Exists(Path.Combine(root,"V15Updater.exe")))throw new Exception("Thiếu công cụ cập nhật. Dùng CAP_NHAT.cmd trong ZIP lần đầu.");
  SaveSession();string folder=Path.Combine(Path.GetTempPath(),"V15_update_runner_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string updater=Path.Combine(folder,"V15Updater.exe");File.Copy(Path.Combine(root,"V15Updater.exe"),updater);
  Process.Start(new ProcessStartInfo(updater,"--zip "+KokoroEngine.Q(d.FileName)+" --target "+KokoroEngine.Q(root)+" --parent "+Process.GetCurrentProcess().Id+" --restart"){UseShellExecute=false});Close();
 }catch(Exception e){Error(e);}}
}
}
