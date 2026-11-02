using System;using System.IO;using System.Linq;using System.Text;using System.Diagnostics;
namespace V15Voice {
public static class NativeGpu {
 public static string Folder(string root){return Path.Combine(root,"engine","gpu");}
 public static string Executable(string root){return Path.Combine(Folder(root),Environment.OSVersion.Platform==PlatformID.Win32NT?"sherpa-onnx-offline-tts.exe":"sherpa-onnx-offline-tts");}
 public static bool Installed(string root){return File.Exists(Executable(root))&&(Environment.OSVersion.Platform!=PlatformID.Win32NT||new[]{"onnxruntime.dll","onnxruntime_providers_cuda.dll","onnxruntime_providers_shared.dll"}.All(n=>File.Exists(Path.Combine(Folder(root),n))));}
 public static bool FellBack(string line){string s=(line??"").ToLowerInvariant();return s.Contains("fallback to cpu")||s.Contains("falling back to cpu")||s.Contains("failed to create cudaexecutionprovider")||s.Contains("cudaexecutionprovider is not in available provider")||s.Contains("please compile with -dsherpa_onnx_enable_gpu=on");}
 public static Wave Generate(KokoroEngine e,string arguments,string output){
  bool installed=Installed(e.Root),gpu=e.DeviceMode!="cpu"&&installed&&(e.DeviceMode!="auto"||!e.NativeForcedCpu);
  if(e.DeviceMode=="gpu"&&!gpu)throw new Exception("Chưa có gói GPU ONNX. Đặt đủ 4 phần V15_ONNX_GPU_1.4.3 cạnh nhau, chọn phần 01 tại Công cụ > Cập nhật từ ZIP.");
  string reason=!installed?"Chưa có gói GPU ONNX":e.DeviceMode=="auto"&&e.NativeForcedCpu?e.NativeFallbackReason:"";
  for(int attempt=0;attempt<2;attempt++){
   if(e.Cancelled)throw new OperationCanceledException();if(File.Exists(output))File.Delete(output);
   var info=new ProcessStartInfo(gpu?Executable(e.Root):e.Executable,"--provider="+(gpu?"cuda":"cpu")+" "+arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=e.Root};
   if(gpu)info.EnvironmentVariables["PATH"]=Folder(e.Root)+Path.PathSeparator+Path.Combine(e.Root,"ai","gpu","site-packages","torch","lib")+Path.PathSeparator+(info.EnvironmentVariables["PATH"]??"");
   bool fallback=false;var errors=new StringBuilder();object gate=new object();int code=-1;Exception failure=null;
   using(var p=new Process{StartInfo=info}){
    DataReceivedEventHandler read=(s,x)=>{if(x.Data==null)return;lock(gate){if(FellBack(x.Data))fallback=true;errors.AppendLine(x.Data);if(errors.Length>16000)errors.Remove(0,errors.Length-16000);}};
    p.ErrorDataReceived+=read;p.OutputDataReceived+=read;
    try{p.Start();p.BeginErrorReadLine();p.BeginOutputReadLine();var timer=Stopwatch.StartNew();while(!p.WaitForExit(100)){if(e.Cancelled||timer.Elapsed.TotalMinutes>10){try{p.Kill();}catch{}if(e.Cancelled)throw new OperationCanceledException();throw new Exception("Bộ đọc ONNX chờ quá 10 phút.");}}p.WaitForExit();code=p.ExitCode;}catch(OperationCanceledException){throw;}catch(Exception x){failure=x;}
   }
   if(e.Cancelled)throw new OperationCanceledException();string message;lock(gate)message=errors.ToString();
   if(failure==null&&code==0&&File.Exists(output)&&!fallback){var w=Wave.Read(output).TrimEdges();e.ReportDevice(gpu?"NVIDIA CUDA · "+e.Kind+" · đã tạo WAV (một số toán tử có thể dùng CPU)":"CPU · "+e.Kind+(e.DeviceMode=="auto"?" · "+reason:" · đã chọn thủ công"));return w;}
   if(e.RunReport!=null)e.RunReport.ProcessFailure(gpu?"ONNX CUDA":"ONNX CPU",failure==null?(int?)code:null,message,failure,gpu&&e.DeviceMode=="auto"&&attempt==0);if(gpu&&e.DeviceMode=="auto"&&attempt==0){reason="GPU ONNX lỗi, dùng CPU dự phòng";e.NativeForcedCpu=true;e.NativeFallbackReason=reason;e.ReportDevice(reason);gpu=false;continue;}
   if(File.Exists(output))File.Delete(output);throw new Exception((gpu?"Không thể tạo giọng bằng CUDA. ":"Bộ đọc ONNX gặp lỗi. ")+(fallback?"Engine chuyển về CPU; chế độ bắt buộc GPU từ chối kết quả này. ":"")+"Mã tiến trình 0x"+code.ToString("X8")+". "+(failure==null?message:failure.Message));
  }throw new Exception("Không tạo được âm thanh ONNX.");
 }
}
}
