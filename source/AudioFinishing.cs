using System;using System.IO;using System.Text;using System.Text.RegularExpressions;using System.Diagnostics;using System.Globalization;
namespace V15Voice {
public static class AudioProcess {
 public static string Run(string root,string args,Func<bool> cancel,int timeout=120000){
  if(cancel())throw new OperationCanceledException();string log="";var gate=new object();
  var si=new ProcessStartInfo(Tempo.Executable(root),"-nostdin -hide_banner -nostats "+args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
  using(var p=new Process{StartInfo=si}){p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(gate){log+=e.Data+"\n";if(log.Length>262144)log=log.Substring(log.Length-262144);}};p.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lock(gate){log+=e.Data+"\n";if(log.Length>262144)log=log.Substring(log.Length-262144);}};
   p.Start();p.BeginErrorReadLine();p.BeginOutputReadLine();var clock=Stopwatch.StartNew();
   while(!p.WaitForExit(100)){if(cancel()||clock.ElapsedMilliseconds>timeout){try{p.Kill();p.WaitForExit(2000);}catch{}if(cancel())throw new OperationCanceledException();throw new TimeoutException("Xử lý âm thanh quá thời gian cho phép.");}}
   p.WaitForExit();if(cancel())throw new OperationCanceledException();if(p.ExitCode!=0){var ex=new IOException("Không xử lý được âm thanh: "+log.Substring(Math.Max(0,log.Length-2500)));ex.Data["ProcessExitCode"]=p.ExitCode;ex.Data["Process"]=si.FileName;ex.Data["CapturedOutput"]=log;throw ex;}return log;
  }
 }
}
public sealed class LoudnessStats {
 public double Integrated,TruePeak,Range,Threshold,Offset;
 static double Number(string log,string name){var m=Regex.Match(log,"\\\""+name+"\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");if(!m.Success)throw new IOException("Thiếu kết quả đo âm lượng: "+name);string s=m.Groups[1].Value;if(s=="-inf")return Double.NegativeInfinity;if(s=="inf")return Double.PositiveInfinity;double v;if(!Double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||Double.IsNaN(v))throw new IOException("Kết quả đo âm lượng không hợp lệ: "+name);return v;}
 public static LoudnessStats Parse(string log){int i=log.LastIndexOf('{');if(i<0)throw new IOException("Không nhận được kết quả đo âm lượng.");log=log.Substring(i);return new LoudnessStats{Integrated=Number(log,"input_i"),TruePeak=Number(log,"input_tp"),Range=Number(log,"input_lra"),Threshold=Number(log,"input_thresh"),Offset=Number(log,"target_offset")};}
}
public static class FinalAudio {
 static string N(double x){return x.ToString("0.000000",CultureInfo.InvariantCulture);}
 public static void Validate(RenderOptions o){if(Double.IsNaN(o.TargetLufs)||Double.IsInfinity(o.TargetLufs)||o.TargetLufs< -24||o.TargetLufs> -14)throw new Exception("Âm lượng đích cần nằm trong -24 đến -14 LUFS.");}
 public static LoudnessStats Measure(string root,string input,Func<bool> cancel,int timeout=120000,double target=-18){return LoudnessStats.Parse(AudioProcess.Run(root,"-i "+KokoroEngine.Q(input)+" -af loudnorm=I="+N(target)+":TP=-1:LRA=11:print_format=json -f null -",cancel,timeout));}
 public static long PcmFrames(string path){using(var f=new BinaryReader(File.OpenRead(path))){if(new string(f.ReadChars(4))!="RIFF")throw new IOException("WAV không có RIFF.");f.ReadUInt32();if(new string(f.ReadChars(4))!="WAVE")throw new IOException("WAV không hợp lệ.");bool format=false;while(f.BaseStream.Position+8<=f.BaseStream.Length){string id=new string(f.ReadChars(4));uint size=f.ReadUInt32();long next=f.BaseStream.Position+size+(size%2);if(next>f.BaseStream.Length)throw new IOException("WAV bị thiếu dữ liệu.");if(id=="fmt "){if(size<16)throw new IOException("WAV thiếu định dạng.");format=f.ReadInt16()==1&&f.ReadInt16()==1&&f.ReadInt32()==24000;f.ReadInt32();format=format&&f.ReadInt16()==2&&f.ReadInt16()==16;}else if(id=="data"){if(!format||size%2!=0)throw new IOException("Kết quả phải là WAV PCM mono 24 kHz/16-bit.");return size/2;}f.BaseStream.Position=next;}throw new IOException("WAV thiếu dữ liệu âm thanh.");}}
 public static void Normalize(string root,string input,string output,long frames,double target,Func<bool> cancel,Action<string> activity){
  Validate(new RenderOptions{TargetLufs=target});if(File.Exists(output))throw new IOException("File trung gian đã tồn tại.");int timeout=(int)Math.Min(3600000L,Math.Max(120000L,frames/24000L*3000L));
  if(activity!=null)activity("Cân âm lượng · đang đo WAV");var s=Measure(root,input,cancel,timeout,target);
  try{
   if(Double.IsNegativeInfinity(s.Integrated)){if(cancel())throw new OperationCanceledException();File.Copy(input,output,false);}
   else{if(Double.IsInfinity(s.TruePeak)||Double.IsInfinity(s.Range)||Double.IsInfinity(s.Threshold)||Double.IsInfinity(s.Offset))throw new IOException("Không thể cân âm lượng với kết quả đo này.");
    if(activity!=null)activity("Cân âm lượng · "+s.Integrated.ToString("0.0",CultureInfo.InvariantCulture)+" → "+target.ToString("0.0",CultureInfo.InvariantCulture)+" LUFS");
    string filter="loudnorm=I="+N(target)+":TP=-1:LRA=11:measured_I="+N(s.Integrated)+":measured_TP="+N(s.TruePeak)+":measured_LRA="+N(s.Range)+":measured_thresh="+N(s.Threshold)+":offset="+N(s.Offset)+":linear=true:print_format=json,aresample=24000,apad=whole_len="+frames+",atrim=end_sample="+frames+",asetpts=PTS-STARTPTS";
    AudioProcess.Run(root,"-v info -y -i "+KokoroEngine.Q(input)+" -af "+KokoroEngine.Q(filter)+" -ac 1 -ar 24000 -c:a pcm_s16le "+KokoroEngine.Q(output),cancel,timeout);
   }
   if(cancel())throw new OperationCanceledException();if(PcmFrames(output)!=frames)throw new IOException("Cân âm lượng làm thay đổi độ dài WAV; chưa xuất kết quả.");
  }catch{try{File.Delete(output);}catch{}throw;}
 }
}
}
