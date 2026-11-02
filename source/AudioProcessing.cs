using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
namespace V15Voice {
public static class Tempo {
 public static double Minimum(RenderOptions o){return Math.Max(.90,o.GentleFit?Math.Max(o.MinSpeed,o.BaseSpeed*.9):o.MinSpeed);}
 public static double Maximum(RenderOptions o){return Math.Min(1.25,o.GentleFit?Math.Min(o.MaxSpeed,o.BaseSpeed*1.1):o.MaxSpeed);}
 public static void Validate(RenderOptions o){foreach(double n in new[]{o.BaseSpeed,o.MinSpeed,o.MaxSpeed})if(Double.IsNaN(n)||Double.IsInfinity(n))throw new Exception("Giới hạn tốc độ không hợp lệ.");if(o.BaseSpeed<.8||o.BaseSpeed>1.3||o.MinSpeed<.5||o.MinSpeed>o.BaseSpeed||o.MaxSpeed<o.BaseSpeed||o.MaxSpeed>1.6)throw new Exception("Tốc độ: 0,50 ≤ tối thiểu ≤ gốc ≤ tối đa ≤ 1,60; tốc độ gốc 0,80–1,30.");}
 public static string Executable(string root){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return Environment.GetEnvironmentVariable("V15_FFMPEG")??"ffmpeg";string bundled=Path.Combine(root,"source","media","ffmpeg.exe");return File.Exists(bundled)?bundled:Path.Combine(root,"ai","ffmpeg.exe");}
 public static Wave Change(Wave original,double factor,string root,string work,Func<bool> cancel){
  if(Math.Abs(factor-1)<.00001)return original;
  if(factor<.25||factor>2)throw new Exception("Tỷ lệ đổi tốc độ nằm ngoài phạm vi hỗ trợ.");
  var perf=RenderPerformance.Current;using(perf==null?null:perf.Time("ATEMPO_PROCESS")){if(perf!=null)perf.Count("ATEMPO_REQUEST");
  string input=Path.Combine(work,"tempo-in.wav"),output=Path.Combine(work,"tempo-out.wav");original.Write(input);
  string filter=factor<.5?"atempo=0.5,atempo="+(factor*2).ToString("0.000000",CultureInfo.InvariantCulture):"atempo="+factor.ToString("0.000000",CultureInfo.InvariantCulture);
  var si=new ProcessStartInfo(Executable(root),"-nostdin -v error -y -i "+KokoroEngine.Q(input)+" -af "+filter+" -ac 1 -ar 24000 -c:a pcm_s16le "+KokoroEngine.Q(output)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
  string error="";using(var p=new Process{StartInfo=si}){p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null&&error.Length<4000)error+=e.Data+"\n";};p.OutputDataReceived+=(s,e)=>{};p.Start();p.BeginErrorReadLine();p.BeginOutputReadLine();var timer=Stopwatch.StartNew();
   while(!p.WaitForExit(100)){if(cancel()||timer.Elapsed.TotalMinutes>2){try{p.Kill();}catch{}if(cancel())throw new OperationCanceledException();throw new Exception("Đổi tốc độ chờ quá lâu.");}}
   p.WaitForExit();if(cancel())throw new OperationCanceledException();if(p.ExitCode!=0){var ex=new Exception("Không thể đổi tốc độ: "+error);ex.Data["ProcessExitCode"]=p.ExitCode;ex.Data["Process"]=si.FileName;ex.Data["CapturedStderr"]=error;throw ex;}
  }return Wave.Read(output).TrimEdges();}
 }
 public static Wave Fit(Wave source,Cue cue,RenderOptions options,string root,string work,Func<bool> cancel){
  Validate(options);double available=Math.Max(.04,cue.Duration-.025),minimum=Minimum(options),maximum=Maximum(options);double selected=options.BaseSpeed;Wave result=source;bool fast=source.Duration>available;if(options.RunReport!=null)options.RunReport.FitBudget(source.Duration,available);if(options.RunReport!=null)options.RunReport.Attempt(options.BaseSpeed,source.Duration,available,"Đầu vào trước đổi tốc độ (đã chỉnh khoảng nghỉ nếu có)");
  if(fast)selected=Math.Min(maximum,Math.Max(options.BaseSpeed,options.BaseSpeed*source.Duration/available*1.01));
  else if(options.SlowShort&&source.Duration/available<.9)selected=Math.Max(minimum,Math.Min(options.BaseSpeed,options.BaseSpeed*source.Duration/available*1.01));
  if(Math.Abs(selected-options.BaseSpeed)>.001){result=Change(source,selected/options.BaseSpeed,root,work,cancel);if(options.RunReport!=null)options.RunReport.Attempt(selected,result.Duration,available,"Đã đo sau atempo");}
  // Retry only from the original waveform; never compound stretch artifacts.
  for(int retry=0;result.Duration>available&&selected<maximum-.00001&&retry<3;retry++){
   selected=Math.Min(maximum,Math.Max(selected+.01,selected*result.Duration/available*1.01));result=Change(source,selected/options.BaseSpeed,root,work,cancel);if(options.RunReport!=null)options.RunReport.Attempt(selected,result.Duration,available,"Thử lại từ audio gốc");
  }
  cue.Speed=selected;cue.Actual=result.Duration;return result;
 }
}
public sealed class StreamingWave : IDisposable {
 readonly BinaryWriter writer;readonly int rate;long frames;
 public StreamingWave(string path,int sampleRate){rate=sampleRate;writer=new BinaryWriter(new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None));writer.Write(new byte[44]);}
 void Pad(long target){if(target<frames)throw new Exception("Âm thanh chồng lên đoạn trước.");var zeros=new byte[48000];while(frames<target){int n=(int)Math.Min(zeros.Length/2,target-frames);writer.Write(zeros,0,n*2);frames+=n;}}
 public void At(double seconds,Wave wave){long start=(long)Math.Round(seconds*rate);Pad(start);Wave w=wave.Resample(rate);byte[] data=new byte[w.Samples.Length*2];Buffer.BlockCopy(w.Samples,0,data,0,data.Length);writer.Write(data);frames+=w.Samples.Length;}
 public void Finish(double end){long total=(long)Math.Round(end*rate);if(total*2>UInt32.MaxValue-36)throw new Exception("WAV vượt giới hạn định dạng RIFF.");Pad(total);writer.Seek(0,SeekOrigin.Begin);writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write((uint)(36+total*2));writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write((uint)(total*2));writer.Flush();}
 public void Dispose(){writer.Dispose();}
}
}
