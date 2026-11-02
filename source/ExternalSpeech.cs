using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace V15Voice {
public sealed class ExternalSpeech : IDisposable {
 readonly KokoroEngine owner;Process process;readonly Queue<string> replies=new Queue<string>();readonly AutoResetEvent signal=new AutoResetEvent(false);string work;string errors="";IntPtr job=IntPtr.Zero;bool forceCpu,disposed;int workerEpoch,seedOffset;readonly int startupTimeoutMs,synthesisTimeoutMs;string lastActivity="Đang chờ AI";
 public ExternalSpeech(KokoroEngine engine,int startupTimeoutMilliseconds=600000,int synthesisTimeoutMilliseconds=0){owner=engine;startupTimeoutMs=Math.Max(50,startupTimeoutMilliseconds);synthesisTimeoutMs=synthesisTimeoutMilliseconds;}
 public static string Python(string root){return Environment.OSVersion.Platform==PlatformID.Win32NT?Path.Combine(root,"ai","runtime","python.exe"):(Environment.GetEnvironmentVariable("V15_PYTHON")??"python3");}
 public static string B64(string s){return Convert.ToBase64String(Encoding.UTF8.GetBytes(s??""));}
 public static string Decode(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s));}
 public static void Check(KokoroEngine e){
  if(e.Kind==EngineKind.VieNeu){VietnamesePack.Check(e);return;}
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
 void Activity(string text){lastActivity=text;owner.ReportActivity((String.IsNullOrEmpty(owner.RenderContext)?"":owner.RenderContext+" · ")+text);}
 void Start(){
  if(disposed)throw new ObjectDisposedException("ExternalSpeech");
  if(process!=null&&!process.HasExited)return;
  StopWorker();Check(owner);work=Path.Combine(Path.GetTempPath(),"V15_ai_owner_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
  string worker=owner.Kind==EngineKind.VieNeu?VietnamesePack.Script(owner.Root,"vi_worker.py"):Path.Combine(owner.Root,"ai","worker.py");
  string args=KokoroEngine.Q(worker)+" --device "+(forceCpu?"cpu":owner.DeviceMode)+" --model "+owner.ModelFolder+" --speaker "+KokoroEngine.Q(owner.VoiceName??"Aiden")+" --threads "+owner.Threads+" --work-dir "+KokoroEngine.Q(work)+" --seed-offset "+seedOffset;
  if(!String.IsNullOrEmpty(owner.ReferencePath))args+=" --reference "+KokoroEngine.Q(owner.ReferencePath);
  if(!String.IsNullOrEmpty(owner.ReferenceText))args+=" --ref-text "+KokoroEngine.Q(owner.ReferenceText);
  if(owner.Kind==EngineKind.VieNeu)args+=" --home "+KokoroEngine.Q(VietnamesePack.Home())+" --ffmpeg "+KokoroEngine.Q(Tempo.Executable(owner.Root));
  var pi=new ProcessStartInfo(owner.Kind==EngineKind.VieNeu?VietnamesePack.Python():Python(owner.Root),args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=owner.Root};
  pi.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";pi.EnvironmentVariables["HF_HUB_OFFLINE"]="1";pi.EnvironmentVariables["TRANSFORMERS_OFFLINE"]="1";
  int epoch=Interlocked.Increment(ref workerEpoch);var started=new Process{StartInfo=pi};process=started;
  started.OutputDataReceived+=(s,e)=>{if(e.Data==null||!e.Data.StartsWith("V15_"))return;lock(replies){if(epoch!=workerEpoch)return;replies.Enqueue(e.Data);}try{signal.Set();}catch(ObjectDisposedException){};};
  started.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(replies){if(epoch!=workerEpoch)return;errors+=e.Data+"\n";if(errors.Length>6000)errors=errors.Substring(errors.Length-6000);}};
  started.Start();AttachJob();started.StandardInput.AutoFlush=true;started.BeginOutputReadLine();started.BeginErrorReadLine();Activity("Đang nạp model và mẫu giọng");
  string result=Wait(startupTimeoutMs);if(!result.StartsWith("V15_READY\t"))Throw(result);owner.ReportDevice((forceCpu?"CPU dự phòng sau lỗi GPU · ":"")+Decode(result.Split('\t')[1]));
 }
 public static int GenerationTimeoutMilliseconds(string text,bool cpu){int words=Math.Max(1,(text??"").Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Length),segments=(words+23)/24;long attempt=Math.Max(cpu?90000:45000,words*(cpu?12000L:4000L)+segments*(cpu?45000L:20000L));return (int)Math.Min(cpu?1800000L:600000L,(cpu?120000L:90000L)+3*attempt);}

 string Wait(int timeout){var timer=Stopwatch.StartNew();long pulse=-2;while(true){
  if(owner.Cancelled){Cancel();throw new OperationCanceledException();}
  string line=null;lock(replies){if(replies.Count>0)line=replies.Dequeue();}
  if(line!=null){if(line.StartsWith("V15_STATUS\t")){Activity(Decode(line.Substring(11)));}else return line;}
  var running=process;
  if(running==null)throw new IOException("Tiến trình AI đã đóng.");
  if(running.HasExited){running.WaitForExit();lock(replies){if(replies.Count>0)continue;}throw new IOException("Tiến trình AI đã dừng (mã "+running.ExitCode+"). "+errors);}
  if(timer.ElapsedMilliseconds>=timeout)throw new TimeoutException("Tiến trình AI không hoàn tất trong "+(timeout/1000)+" giây. Thiết bị: "+(String.IsNullOrEmpty(owner.DeviceInfo)?(forceCpu?"CPU":owner.DeviceMode):owner.DeviceInfo));
  if(timer.ElapsedMilliseconds/1000>=pulse+2){pulse=timer.ElapsedMilliseconds/1000;owner.ReportActivity((String.IsNullOrEmpty(owner.RenderContext)?"":owner.RenderContext+" · ")+lastActivity+" · "+pulse+"s / "+(timeout/1000)+"s");}
  signal.WaitOne(100);
 }}
 void Throw(string reply){string[] p=reply.Split('\t');throw new Exception("Bộ giọng AI: "+(p.Length>1?Decode(p[1]):reply));}
 string LogFailure(Exception error){try{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"V15VoiceStudio","logs");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"ai-errors.log");if(File.Exists(file)&&new FileInfo(file).Length>131072)File.WriteAllText(file,"");File.AppendAllText(file,DateTime.UtcNow.ToString("o")+" · "+owner.RenderContext+" · "+owner.Kind+" · "+owner.DeviceInfo+"\r\n"+error+"\r\n"+errors+"\r\n");return file;}catch{return "";}}
 public Wave Generate(string text,double speed,string output){int restarts=0;string previousError="";
  for(int round=0;round<3;round++)try{
   Start();Activity("Đang tổng hợp giọng · "+owner.DeviceInfo);process.StandardInput.WriteLine("SYNTH\t"+B64(text)+"\t"+B64(output)+"\t"+speed.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture));
   bool cpu=forceCpu||owner.DeviceMode=="cpu"||owner.DeviceInfo.StartsWith("CPU");string result=Wait(synthesisTimeoutMs>0?synthesisTimeoutMs:GenerationTimeoutMilliseconds(text,cpu));if(!result.StartsWith("V15_OK\t"))Throw(result);
   owner.ReportDevice((forceCpu?"CPU dự phòng sau lỗi GPU · ":"")+Decode(result.Split('\t')[1]));return Wave.Read(output).TrimEdges();
  }catch(Exception e){
   if(owner.Cancelled||e is OperationCanceledException){StopWorker();throw new OperationCanceledException();}
   bool fallback=owner.DeviceMode=="auto"&&!forceCpu&&e.Message.Contains("GPU_FALLBACK:");bool restart=(e is TimeoutException||e is IOException)&&restarts<1;string diagnostic="";
   if(round>=2||(!fallback&&!restart))diagnostic=LogFailure(e);
   previousError=e.Message;StopWorker();seedOffset++;
   if(round<2&&fallback){forceCpu=true;owner.ReportDevice("CPU dự phòng sau lỗi GPU");Activity("GPU gặp lỗi · nạp lại model trên CPU và tạo lại câu hiện tại");continue;}
   if(round<2&&restart){restarts++;Activity("AI không phản hồi · khởi động lại và thử lại câu hiện tại (1/1)");continue;}
   string context=String.IsNullOrEmpty(owner.RenderContext)?"Tạo giọng":owner.RenderContext;
   throw new Exception(context+" · "+owner.Kind+" · "+owner.DeviceInfo+"\r\n"+e.Message+(diagnostic.Length>0?"\r\nNhật ký: "+diagnostic:""),e);
  }throw new Exception("Không thể tạo âm thanh. "+previousError);
 }
 public void Cancel(){try{if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}if(process!=null&&!process.HasExited)process.Kill();}catch{}try{signal.Set();}catch(ObjectDisposedException){}}
 void StopWorker(){Interlocked.Increment(ref workerEpoch);Cancel();var old=process;process=null;if(old!=null){try{old.WaitForExit(2000);}catch{}old.Dispose();}lock(replies){replies.Clear();errors="";}if(work!=null){try{Directory.Delete(work,true);}catch{}work=null;}}
 public void Dispose(){if(disposed)return;StopWorker();disposed=true;signal.Dispose();}
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
