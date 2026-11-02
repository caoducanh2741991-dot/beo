using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace V15Voice {
public sealed class ExternalSpeech : IDisposable {
 readonly KokoroEngine owner;Process process;readonly Queue<string> replies=new Queue<string>();readonly AutoResetEvent signal=new AutoResetEvent(false);string work;string errors="";IntPtr job=IntPtr.Zero;bool forceCpu;
 public ExternalSpeech(KokoroEngine engine){owner=engine;}
 public static string Python(string root){return Environment.OSVersion.Platform==PlatformID.Win32NT?Path.Combine(root,"ai","runtime","python.exe"):(Environment.GetEnvironmentVariable("V15_PYTHON")??"python3");}
 public static string B64(string s){return Convert.ToBase64String(Encoding.UTF8.GetBytes(s??""));}
 public static string Decode(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
 public static void Check(KokoroEngine e){
  string model=Path.Combine(e.Root,"ai","models",e.ModelFolder);
  var required=new List<string>{Path.Combine(e.Root,"ai","worker.py"),Path.Combine(e.Root,"ai","ffmpeg.exe")};
  if(Environment.OSVersion.Platform==PlatformID.Win32NT)required.Add(Python(e.Root));
  if(e.Kind==EngineKind.Qwen){
   if(e.ModelFolder!="qwen_custom"&&e.ModelFolder!="qwen_base")throw new Exception("Bộ giọng Qwen không hợp lệ.");
   required.AddRange(new[]{Path.Combine(model,"model.safetensors"),Path.Combine(model,"config.json"),Path.Combine(e.Root,"ai","models","qwen_custom","speech_tokenizer","model.safetensors")});
   if(e.ModelFolder=="qwen_base"&&String.IsNullOrEmpty(e.ReferencePath))throw new Exception("Giọng sao chép chưa có mẫu tham chiếu.");
  }else{if(e.ModelFolder!="chatter_nano")throw new Exception("Bộ giọng Chatterbox không hợp lệ.");required.AddRange(new[]{Path.Combine(model,"t3_nano_v1.safetensors"),Path.Combine(model,"s3gen_meanflow.safetensors"),Path.Combine(model,"ve.safetensors"),Path.Combine(model,"conds.pt")});}
  if(!String.IsNullOrEmpty(e.ReferencePath))required.Add(e.ReferencePath);
  foreach(string file in required)if(!File.Exists(file))throw new Exception("Thiếu bộ giọng AI offline: "+file+". Cài đủ các gói dữ liệu 1.4.");
 }
 void Start(){
  if(process!=null)return;Check(owner);
  work=Path.Combine(Path.GetTempPath(),"V15_ai_owner_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
  string args=KokoroEngine.Q(Path.Combine(owner.Root,"ai","worker.py"))+" --device "+(forceCpu?"cpu":owner.DeviceMode)+" --model "+owner.ModelFolder+" --speaker "+KokoroEngine.Q(owner.VoiceName??"Aiden")+" --threads "+owner.Threads+" --work-dir "+KokoroEngine.Q(work);
  if(!String.IsNullOrEmpty(owner.ReferencePath))args+=" --reference "+KokoroEngine.Q(owner.ReferencePath);
  if(!String.IsNullOrEmpty(owner.ReferenceText))args+=" --ref-text "+KokoroEngine.Q(owner.ReferenceText);
  var pi=new ProcessStartInfo(Python(owner.Root),args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=owner.Root};
  pi.EnvironmentVariables["HF_HUB_OFFLINE"]="1";pi.EnvironmentVariables["TRANSFORMERS_OFFLINE"]="1";
  process=new Process{StartInfo=pi};process.OutputDataReceived+=(s,e)=>{if(e.Data!=null&&e.Data.StartsWith("V15_")){lock(replies)replies.Enqueue(e.Data);signal.Set();}};
  process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null){lock(replies){errors+=e.Data+"\n";if(errors.Length>6000)errors=errors.Substring(errors.Length-6000);}}};
  process.Start();AttachJob();process.StandardInput.AutoFlush=true;process.BeginOutputReadLine();process.BeginErrorReadLine();
  string result=Wait();if(!result.StartsWith("V15_READY\t"))Throw(result);owner.ReportDevice((forceCpu?"CPU dự phòng sau lỗi GPU · ":"")+Decode(result.Split('\t')[1]));
 }
 string Wait(){var timer=Stopwatch.StartNew();while(true){
  if(owner.Cancelled){Cancel();throw new OperationCanceledException();}
  lock(replies){if(replies.Count>0)return replies.Dequeue();}
  if(process==null||process.HasExited)throw new Exception("Bộ giọng AI đã dừng. "+errors);
  if(timer.Elapsed.TotalMinutes>20){Cancel();throw new Exception("Bộ giọng AI chờ quá lâu. Kiểm tra RAM và thử đoạn ngắn hơn.");}
  signal.WaitOne(100);
 }}
 void Throw(string reply){string[] p=reply.Split('\t');throw new Exception("Bộ giọng AI: "+(p.Length>1?Decode(p[1]):reply));}
 public Wave Generate(string text,double speed,string output){
  for(int attempt=0;attempt<2;attempt++)try{
   Start();process.StandardInput.WriteLine("SYNTH\t"+B64(text)+"\t"+B64(output)+"\t"+speed.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture));string result=Wait();if(!result.StartsWith("V15_OK\t"))Throw(result);
   owner.ReportDevice((forceCpu?"CPU dự phòng sau lỗi GPU · ":"")+Decode(result.Split('\t')[1]));return Wave.Read(output).TrimEdges();
  }catch(Exception e){if(owner.Cancelled)throw new OperationCanceledException();if(attempt!=0||forceCpu||owner.DeviceMode!="auto"||!e.Message.Contains("GPU_FALLBACK:"))throw;
   owner.ReportDevice("GPU gặp lỗi · đang chuyển sang CPU và tạo lại câu hiện tại");Dispose();lock(replies){replies.Clear();errors="";}forceCpu=true;
  }throw new Exception("Không thể tạo âm thanh.");
 }
 public void Cancel(){try{if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}if(process!=null&&!process.HasExited)process.Kill();}catch{}signal.Set();}
 public void Dispose(){Cancel();if(process!=null){try{process.WaitForExit(2000);}catch{}process.Dispose();process=null;}if(work!=null){try{Directory.Delete(work,true);}catch{}}}
 // Windows job objects also terminate any audio-conversion child process on cancellation.
 [StructLayout(LayoutKind.Sequential)]struct BASIC {public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
 [StructLayout(LayoutKind.Sequential)]struct IO {public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
 [StructLayout(LayoutKind.Sequential)]struct LIMIT {public BASIC BasicLimitInformation;public IO IoInfo;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateJobObject(IntPtr attr,string name);
 [DllImport("kernel32.dll")]static extern bool SetInformationJobObject(IntPtr job,int type,IntPtr info,uint length);
 [DllImport("kernel32.dll")]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 void AttachJob(){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;IntPtr p=IntPtr.Zero;try{job=CreateJobObject(IntPtr.Zero,null);var limits=new LIMIT();limits.BasicLimitInformation.LimitFlags=0x2000;int size=Marshal.SizeOf(limits);p=Marshal.AllocHGlobal(size);Marshal.StructureToPtr(limits,p,false);if(!SetInformationJobObject(job,9,p,(uint)size)||!AssignProcessToJobObject(job,process.Handle)){CloseHandle(job);job=IntPtr.Zero;}}catch{if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}}finally{if(p!=IntPtr.Zero)Marshal.FreeHGlobal(p);}}
}
}
