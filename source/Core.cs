using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace V15Voice {
public sealed class Cue {
 public int Id; public double Start, End; public string Text; public bool Prose,ParagraphEnd,AnchorAfter;public string VoiceLock="";
 public double Natural, Actual, Speed=1; public string Status="Chưa tạo"; public string AudioPath;
 public int Words { get { return Regex.Matches(Regex.Replace(Text??"", @"<[^>]*>", ""), @"[\p{L}\p{N}]+(?:['’.-][\p{L}\p{N}]+)*").Count; } }
 public double Duration { get { return End-Start; } }
}
public static class Scripts {
 public const int MaxCues=20000; public const int MaxSeconds=21600;
 public static double Time(string s) {
  var m=Regex.Match(s.Trim(), @"^(\d{1,3}):(\d{2}):(\d{2})[,.](\d{3})$");
  if(!m.Success || int.Parse(m.Groups[2].Value)>59 || int.Parse(m.Groups[3].Value)>59) throw new Exception("Timecode không hợp lệ: "+s);
  return int.Parse(m.Groups[1].Value)*3600+int.Parse(m.Groups[2].Value)*60+int.Parse(m.Groups[3].Value)+int.Parse(m.Groups[4].Value)/1000.0;
 }
 public static string Stamp(double s) {
  long ms=(long)Math.Round(s*1000); if(ms<0) throw new Exception("Timecode âm.");
  return String.Format(CultureInfo.InvariantCulture,"{0:00}:{1:00}:{2:00},{3:000}",ms/3600000,(ms/60000)%60,(ms/1000)%60,ms%1000);
 }
 public static List<Cue> ParseSrt(string text) {
  var result=new List<Cue>();
  foreach(var block in Regex.Split(text.Trim('\uFEFF',' ','\r','\n','\t').Replace("\r\n","\n").Replace("\r","\n"), @"\n[ \t]*\n+")) {
   if(String.IsNullOrWhiteSpace(block)) continue;
   var lines=block.Split(new char[] { '\n' }, StringSplitOptions.None); int offset=0, id;
   if(int.TryParse(lines[0].Trim(),out id)) offset=1;
   if(lines.Length<=offset+1) throw new Exception("Khối SRT thiếu lời hoặc timecode: "+block);
   var m=Regex.Match(lines[offset], @"^(\d{1,3}:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(\d{1,3}:\d{2}:\d{2}[,.]\d{3})\s*$");
   if(!m.Success) throw new Exception("Dòng thời gian SRT không hợp lệ: "+lines[offset]);
   string speech=String.Join(" ",lines.Skip(offset+1)).Trim();
   speech=Regex.Replace(speech,@"</?(i|b|u|font)(\s+[^>]*)?>","",RegexOptions.IgnoreCase);
   if(speech.Contains("-->")) throw new Exception("SRT thiếu dòng trống giữa các khối.");
   result.Add(new Cue{Id=result.Count+1,Start=Time(m.Groups[1].Value),End=Time(m.Groups[2].Value),Text=speech});
  }
  Validate(result); return result;
 }
 public static List<Cue> ParseText(string text) {
  if(text.Contains("-->")) return ParseSrt(text);
  var result=new List<Cue>(); double t=0;bool timedInput=Regex.IsMatch(text,@"\d+:\d{2}:\d{2}");
  foreach(var line in text.Replace("\r","").Split(new char[] { '\n' }, StringSplitOptions.None)) {
   if(String.IsNullOrWhiteSpace(line)) continue;
   var timed=Regex.Match(line,@"^\s*\[?(\d{1,3}:\d{2}:\d{2}[,.]\d{3})\s*[-–]\s*(\d{1,3}:\d{2}:\d{2}[,.]\d{3})\]?\s+(.+)$");
   if(timed.Success) {
    result.Add(new Cue{Id=result.Count+1,Start=Time(timed.Groups[1].Value),End=Time(timed.Groups[2].Value),Text=timed.Groups[3].Value}); continue;
   }
   if(timedInput) throw new Exception("TXT có mốc thời gian chưa đúng hoặc trộn dòng chưa có mốc. Dùng [00:00:00.000 - 00:00:03.300] lời đọc cho mọi dòng.");
   foreach(var sentence in Regex.Split(line.Trim(),@"(?<=[.!?])\s+(?=[A-Z""“])")) {
    var words=Regex.Split(sentence.Trim(),@"\s+"); int pos=0;
    while(pos<words.Length) {
     int take=Math.Min(9,words.Length-pos);
     if(words.Length-pos>10) {
      for(int j=take;j>=6;j--) if(Regex.IsMatch(words[pos+j-1],@"[,;:—]$")){take=j;break;}
      if(words.Length-pos-take<=2) take=Math.Max(6,take-2);
     } else take=words.Length-pos;
     string unit=String.Join(" ",words.Skip(pos).Take(take));
     double dur=Math.Max(0.8,take/2.55);
     result.Add(new Cue{Id=result.Count+1,Start=t,End=t+dur,Text=unit}); t+=dur+0.15; pos+=take;
    }
   }
  }
  Validate(result); return result;
 }
 public static void Validate(List<Cue> cues) {
  if(cues.Count==0) throw new Exception("Chưa có lời đọc.");
  if(cues.Count>MaxCues) throw new Exception("Kịch bản có "+cues.Count+" khối; ứng dụng hỗ trợ tối đa 20.000 khối mỗi file.");
  for(int i=0;i<cues.Count;i++) {
   Cue c=cues[i]; c.Id=i+1;
   if(String.IsNullOrWhiteSpace(c.Text)) throw new Exception("Khối "+c.Id+" chưa có lời.");
   if(c.Text.Length>2000) throw new Exception("Khối "+c.Id+" quá dài; hãy chia nhỏ.");
   if(Double.IsNaN(c.Start)||Double.IsNaN(c.End)||Double.IsInfinity(c.Start)||Double.IsInfinity(c.End)||c.Start<0||c.Duration<0.08) throw new Exception("Khối "+c.Id+": thời gian không hợp lệ.");
   if(i>0 && c.Start<cues[i-1].End-0.0001) throw new Exception("Khối "+c.Id+" chồng thời gian với khối trước.");
   if(c.End>MaxSeconds) throw new Exception("Mỗi file hỗ trợ tối đa 6 giờ. Hãy chia phần kịch bản dài hơn.");
  }
 }
 public static string ToSrt(List<Cue> cues) {
  var sb=new StringBuilder(); foreach(var c in cues) sb.Append(c.Id+"\r\n"+Stamp(c.Start)+" --> "+Stamp(c.End)+"\r\n"+c.Text+"\r\n\r\n"); return sb.ToString();
 }
 public static string Advice(Cue c) {
  var a=new List<string>(); if(c.Words>10) a.Add(">10 từ"); if(c.Words<6) a.Add("Câu ngắn");
  if(c.Words/c.Duration>3.2) a.Add("Nhịp đọc gấp");
  if(c.Words/c.Duration<1.5) a.Add("Dễ dư khoảng trống"); return a.Count==0?"V15: hợp lý":String.Join(" · ",a);
 }
}
public sealed class Wave {
 public int Rate; public short[] Samples;
 public double Duration {get{return Samples.Length/(double)Rate;}}
 public static Wave Read(string path) {
  using(var r=new BinaryReader(File.OpenRead(path))) {
   if(new string(r.ReadChars(4))!="RIFF") throw new Exception("Không phải WAV RIFF."); r.ReadInt32();
   if(new string(r.ReadChars(4))!="WAVE") throw new Exception("Không phải WAV.");
   int rate=0,channels=0,bits=0,format=0; byte[] data=null;
   while(r.BaseStream.Position+8<=r.BaseStream.Length) {
    string name=new string(r.ReadChars(4)); int n=r.ReadInt32(); long next=r.BaseStream.Position+n+(n%2);
    if(n<0 || next>r.BaseStream.Length+1) throw new Exception("WAV bị hỏng.");
    if(name=="fmt "){format=r.ReadUInt16(); channels=r.ReadUInt16(); rate=r.ReadInt32();r.ReadInt32();r.ReadUInt16();bits=r.ReadUInt16();}
    else if(name=="data") data=r.ReadBytes(n);
    r.BaseStream.Position=Math.Min(next,r.BaseStream.Length);
   }
   if(format!=1 || bits!=16 || channels!=1 || rate<8000 || data==null || data.Length%2!=0) throw new Exception("Cần WAV PCM 16 bit mono.");
   short[] s=new short[data.Length/2]; Buffer.BlockCopy(data,0,s,0,data.Length); return new Wave{Rate=rate,Samples=s};
  }
 }
 public void Write(string path) {
  using(var w=new BinaryWriter(File.Create(path))) {
   w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+Samples.Length*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(Samples.Length*2);
   byte[] b=new byte[Samples.Length*2];Buffer.BlockCopy(Samples,0,b,0,b.Length);w.Write(b);
  }
 }
 public Wave Resample(int rate){if(rate==Rate)return this;int n=(int)Math.Round(Samples.Length*(double)rate/Rate);var output=new short[n];for(int i=0;i<n;i++){double pos=i*(double)Rate/rate;int a=Math.Min(Samples.Length-1,(int)pos),b=Math.Min(Samples.Length-1,a+1);double f=pos-a;output[i]=(short)Math.Round(Samples[a]*(1-f)+Samples[b]*f);}return new Wave{Rate=rate,Samples=output};}
 public Wave TrimEdges() {
  // Only boundary silence is shortened; quiet consonants keep a 45 ms margin.
  int first=0,last=Samples.Length-1; while(first<Samples.Length && Math.Abs((int)Samples[first])<45) first++;
  while(last>first && Math.Abs((int)Samples[last])<45) last--;
  if(first>=Samples.Length) throw new Exception("Mô hình trả về âm thanh trống.");
  first=Math.Max(0,first-Rate*45/1000); last=Math.Min(Samples.Length-1,last+Rate*65/1000);
  short[] output=new short[last-first+1];Array.Copy(Samples,first,output,0,output.Length);return new Wave{Rate=Rate,Samples=output};
 }
}
public enum EngineKind { Kokoro, LibriTTS, Kitten, Piper, Qwen, Chatterbox, VieNeu, Rvc }
public class KokoroEngine : IDisposable {
 public SpeechCache PreviewCache;public SpeechArchive Archive;public VoiceRenderReport RunReport;
 public Wave GetSpeech(string text,double speed,string output){if(Cancelled)throw new OperationCanceledException();var cached=Archive==null?null:Archive.Get(this,text,speed);if(cached!=null){if(RunReport!=null)RunReport.Origin(true);return cached;}if(Archive==null||!Archive.Refresh)cached=PreviewCache==null?null:PreviewCache.Get(this,text,speed);if(cached!=null){if(RunReport!=null)RunReport.Origin(true);if(Archive!=null)Archive.Put(this,text,speed,cached);return cached;}if(RunReport!=null)RunReport.Origin(false);var wave=Generate(text,speed,output);if(PreviewCache!=null)PreviewCache.Put(this,text,speed,wave);if(Archive!=null)Archive.Put(this,text,speed,wave);return wave;}
 internal bool NativeForcedCpu;internal string NativeFallbackReason;public string VoiceName,ReferencePath,ReferenceText;public string DeviceMode="auto",DeviceInfo="";public Action<string> DeviceChanged,ActivityChanged;public string RenderContext="";ExternalSpeech external;RvcSpeech rvc;
 public void ReportActivity(string text){if(ActivityChanged!=null)ActivityChanged(text);}
 public void ReportDevice(string text){DeviceInfo=text;if(RunReport!=null)RunReport.Device(text);if(DeviceChanged!=null)DeviceChanged(text);}
 public void Dispose(){if(rvc!=null){rvc.Dispose();rvc=null;}if(external!=null){external.Dispose();external=null;}}
 public string Root; public int Speaker=3,Threads=2; public EngineKind Kind=EngineKind.Kokoro;public string ModelFolder="";public bool Libri {get{return Kind==EngineKind.LibriTTS;}set{Kind=value?EngineKind.LibriTTS:EngineKind.Kokoro;}} public volatile bool Cancelled; private Process running;
 public string Executable {get{return Path.Combine(Root,"engine",Environment.OSVersion.Platform==PlatformID.Win32NT?"sherpa-onnx-offline-tts.exe":"sherpa-onnx-offline-tts");}}
 public void Cancel(){Cancelled=true;if(rvc!=null)rvc.Cancel();if(external!=null)external.Cancel();try{if(running!=null&&!running.HasExited)running.Kill();}catch{}}
 public string VoiceFolder {get{if(Kind==EngineKind.Rvc)return Path.Combine(CommunityVoices.RvcHome(Root),"weights");return Path.Combine(Root,Kind==EngineKind.Kokoro?"model":Kind==EngineKind.LibriTTS?"libritts":Kind==EngineKind.Kitten?"kitten":ModelFolder);}}
 public string ModelFile {get{if(Kind==EngineKind.Rvc)return CommunityVoices.RvcModel(Root);return Path.Combine(VoiceFolder,Kind==EngineKind.LibriTTS?"en_US-libritts_r-medium.onnx":"model.onnx");}}
 public virtual void CheckFiles(){
  if(Kind==EngineKind.Rvc){CommunityVoices.CheckRvc(this);return;}
  if(Kind==EngineKind.Qwen||Kind==EngineKind.Chatterbox||Kind==EngineKind.VieNeu){ExternalSpeech.Check(this);return;}
  if(Kind==EngineKind.Piper&&ModelFolder!="piper_john"&&ModelFolder!="piper_kristin")throw new Exception("Bộ giọng Piper chưa được cài hoặc không được hỗ trợ.");
  var required=new List<string>{Executable,ModelFile,Path.Combine(VoiceFolder,"tokens.txt")};
  if(Kind==EngineKind.Kokoro||Kind==EngineKind.Kitten)required.Add(Path.Combine(VoiceFolder,"voices.bin"));
  if(Kind==EngineKind.Kokoro)required.Add(Path.Combine(VoiceFolder,"lexicon-us-en.txt"));
  foreach(string p in required)if(!File.Exists(p))throw new Exception("Thiếu tệp: "+p+". Hãy cài lại bộ đầy đủ.");
  if(!Directory.Exists(Path.Combine(VoiceFolder,"espeak-ng-data")))throw new Exception("Thiếu bộ phát âm offline. Hãy cài lại bộ đầy đủ.");
 }
 // Windows CommandLineToArgvW-compatible quoting, including trailing backslashes.
 public static string Q(string s){var b=new StringBuilder("\"");int slashes=0;foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='\"')b.Append('\\',slashes*2+1);else b.Append('\\',slashes);b.Append(c);slashes=0;}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
 public virtual Wave Generate(string text,double speed,string output) {
  if(Cancelled)throw new OperationCanceledException();
  if(Kind==EngineKind.Rvc){if(rvc==null)rvc=new RvcSpeech(this);return rvc.Generate(text,speed,output);}
  if(Kind==EngineKind.Qwen||Kind==EngineKind.Chatterbox||Kind==EngineKind.VieNeu){if(external==null)external=new ExternalSpeech(this);return external.Generate(text,speed,output);}
  string model=VoiceFolder;
  string args="--debug=0 --num-threads="+Threads+" --sid="+Speaker+" --speed="+speed.ToString("0.000",CultureInfo.InvariantCulture)+" --output-filename="+Q(output)+" ";
  if(Kind==EngineKind.Kokoro)args+="--kokoro-model="+Q(ModelFile)+" --kokoro-voices="+Q(Path.Combine(model,"voices.bin"))+" --kokoro-tokens="+Q(Path.Combine(model,"tokens.txt"))+" --kokoro-data-dir="+Q(Path.Combine(model,"espeak-ng-data"))+" --kokoro-lexicon="+Q(Path.Combine(model,"lexicon-us-en.txt"))+" ";
  else if(Kind==EngineKind.Kitten)args+="--kitten-model="+Q(ModelFile)+" --kitten-voices="+Q(Path.Combine(model,"voices.bin"))+" --kitten-tokens="+Q(Path.Combine(model,"tokens.txt"))+" --kitten-data-dir="+Q(Path.Combine(model,"espeak-ng-data"))+" ";
  else args+="--vits-noise-scale-w=0 --vits-model="+Q(ModelFile)+" --vits-tokens="+Q(Path.Combine(model,"tokens.txt"))+" --vits-data-dir="+Q(Path.Combine(model,"espeak-ng-data"))+" ";
  args+=Q(text);
  return NativeGpu.Generate(this,args,output);
 }
}
public sealed class RenderOptions {
 public double TargetSeconds,TargetChange=.10;public string TargetRaw="";public TextTargetResult TargetResult;public VoiceReportContext ReportContext;public VoiceRenderReport RunReport;
 public Action<string> RepairSnapshot;
 public bool Anchored;public AnchorReport AnchorResult;public Action<AnchorReport> AnchorChanged;public Func<string,List<AnchorUnit>,SubtitleDocument> AnchorAlignment;
 public bool Prose;public Action<ReadingProgress> ReadingChanged;public bool NormalizeLoudness,GentleFit;public double TargetLufs=-18;
 public bool Locked=true,SlowShort=true,JoinSentences=false;public double BaseSpeed=1,MaxSpeed=1.3,MinSpeed=.8,Gap=.15;
 public PunctuationMode Punctuation=PunctuationMode.Auto;public double CommaPause=.14,StopPause=.30,QuestionPause=.35,ParagraphPause=.50;
 public BreathMode Breathing=BreathMode.Auto;public string BreathPath;public double BreathLevel=.16;public int BreathEvery=3;
}
public enum BreathMode { Auto, Custom, None }
public sealed class BreathTrack {
 readonly List<Wave> samples=new List<Wave>();readonly RenderOptions options;int sentences,index;double last=-100;
 public BreathTrack(RenderOptions o,string root){
  options=o;if(o.Breathing==BreathMode.None)return;
  string[] files=o.Breathing==BreathMode.Custom&&!String.IsNullOrWhiteSpace(o.BreathPath)?new[]{o.BreathPath}:Directory.GetFiles(Path.Combine(root,"breaths"),"*.wav").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  if(files.Length==0)throw new Exception("Thiếu bộ mẫu hơi thở. Cài lại bản cập nhật hoặc chọn Không dùng.");
  foreach(string file in files){Wave w=Wave.Read(file).TrimEdges();if(w.Duration>2)throw new Exception("Mẫu hơi thở dài quá 2 giây. Hãy chọn WAV ngắn hơn.");samples.Add(w);}
 }
 public Wave After(Cue cue,int rate,double room=Double.MaxValue){
  if(samples.Count==0||!Regex.IsMatch(cue.Text.TrimEnd(),@"[.!?][""”')\]]?$"))return null;
  sentences++;int every=options.Breathing==BreathMode.Auto?3:Math.Max(1,options.BreathEvery);
  if(sentences<every||(options.Breathing==BreathMode.Auto&&cue.End-last<6))return null;
  // Retry at the next sentence if a fixed timeline has no room; never stretch a sample.
  for(int n=0;n<samples.Count;n++){var sample=samples[(index+n)%samples.Count].Resample(rate);if(sample.Duration+.08<=room)return sample;}return null;
 }
 public void Inserted(Cue cue){sentences=0;last=cue.End;index++;}
 public void Mix(Wave breath,Wave speech,short[] master,int start){
  double voiceRms=Math.Sqrt(speech.Samples.Average(x=>(double)x*x));
  double breathRms=Math.Sqrt(breath.Samples.Average(x=>(double)x*x));
  double level=options.Breathing==BreathMode.Auto?.16:Math.Max(.01,Math.Min(.6,options.BreathLevel));
  double gain=Math.Min(8,voiceRms*level/Math.Max(1,breathRms));int fade=Math.Min(breath.Samples.Length/2,breath.Rate*12/1000);
  for(int j=0;j<breath.Samples.Length;j++){
   double edge=fade==0?1:Math.Min(1,Math.Min(j/(double)fade,(breath.Samples.Length-1-j)/(double)fade));
   master[start+j]=(short)Math.Max(short.MinValue,Math.Min(short.MaxValue,Math.Round(breath.Samples[j]*gain*edge)));
  }
 }
}
public static class OutputFiles {
 public static string Beside(string input){return Path.ChangeExtension(Path.GetFullPath(input),".wav");}
 public static string Publish(string temporary,string requested){
  string dir=Path.GetDirectoryName(Path.GetFullPath(requested)),stem=Path.GetFileNameWithoutExtension(requested);
  for(int n=0;n<10000;n++){
   string file=Path.Combine(dir,stem+(n==0?"":" ("+(n+1)+")")+".wav");
   if(File.Exists(file)||Directory.Exists(file))continue;
   try{File.Move(temporary,file);return file;}catch(IOException){if(File.Exists(file)||Directory.Exists(file))continue;throw;}
  }
  throw new IOException("Có quá nhiều file WAV trùng tên trong thư mục.");
 }
}
public static class Renderer {
 public static string Render(List<Cue> cues,KokoroEngine engine,RenderOptions o,string output,Action<int,string> progress) {
  VoiceRenderReport report=null,prior=engine.RunReport;o.RunReport=null;o.TargetResult=null;
  if(o.ReportContext!=null)try{report=new VoiceRenderReport(cues,engine,o,output);o.RunReport=report;}catch(Exception e){engine.ReportActivity("Không khởi tạo được báo cáo: "+e.Message);}
  engine.RunReport=report;
  try{string final=RenderCore(cues,engine,o,output,progress);if(report!=null)report.Finish("THÀNH CÔNG",final,null);return final;}
  catch(Exception e){if(report!=null)report.Finish(e is OperationCanceledException?"ĐÃ DỪNG":"THẤT BẠI","",e);throw;}
  finally{engine.RunReport=prior;}
 }
 static string RenderCore(List<Cue> cues,KokoroEngine engine,RenderOptions o,string output,Action<int,string> progress) {
  if(o.TargetSeconds!=0)return TextTargetVoice.Render(cues,engine,o,output,progress);if(o.Anchored)return AnchoredVoice.Render(cues,engine,o,output,progress);return RenderNatural(cues,engine,o,output,progress);
 }
 internal static string RenderNatural(List<Cue> cues,KokoroEngine engine,RenderOptions o,string output,Action<int,string> progress){
  if(o.RunReport!=null)o.RunReport.Step("INPUT","Kiểm tra lời và timeline");if(o.Prose)LongText.Validate(cues);else Scripts.Validate(cues);if(o.RunReport!=null)o.RunReport.Step("MODEL","Kiểm tra model/runtime/mẫu giọng");engine.CheckFiles();if(o.RunReport!=null)o.RunReport.Step("INPUT","Kiểm tra thiết lập và đường dẫn xuất");Tempo.Validate(o);FinalAudio.Validate(o);
  if(o.BaseSpeed<.8||o.BaseSpeed>1.3||o.MaxSpeed<o.BaseSpeed||o.MaxSpeed>1.6)throw new Exception("Tốc độ tối đa phải lớn hơn hoặc bằng tốc độ gốc (giới hạn 1,60).");
  if(!String.Equals(Path.GetExtension(output),".wav",StringComparison.OrdinalIgnoreCase))throw new Exception("Kết quả phải là file WAV.");
  output=Path.GetFullPath(output);string parent=Path.GetDirectoryName(output);
  if(!Directory.Exists(parent))throw new DirectoryNotFoundException("Không tìm thấy thư mục nguồn: "+parent);
  string temporary=Path.Combine(parent,".v15_"+Guid.NewGuid().ToString("N")+".tmp"),work=Path.Combine(Path.GetTempPath(),"V15_work_"+Guid.NewGuid().ToString("N"));
  bool failed=false;double cursor=0,minimumCursor=0;var meter=o.Prose?new ReadingMeter(cues,o.BaseSpeed):null;Cue previousCue=null;const int rate=24000;string previousContext=engine.RenderContext;
  try {
   if(o.RunReport!=null)o.RunReport.Step("SETUP","Chuẩn bị thư mục và hơi thở");Directory.CreateDirectory(work);var track=new BreathTrack(o,engine.Root);
   if(o.RunReport!=null)o.RunReport.Step("ASSEMBLE","Mở WAV trung gian");using(var writer=new StreamingWave(temporary,rate)){
    for(int i=0;i<cues.Count;i++){
     if(engine.Cancelled)throw new OperationCanceledException();
     var c=cues[i];if(o.RunReport!=null)o.RunReport.BeginUnit(c.Text);engine.RenderContext=Path.GetFileName(output)+" · khối "+c.Id+"/"+cues.Count+" ["+Scripts.Stamp(c.Start)+"–"+Scripts.Stamp(c.End)+"]";progress(i*90/cues.Count,"Đọc câu "+(i+1)+"/"+cues.Count);
     if(o.Prose&&!String.IsNullOrEmpty(c.VoiceLock)){if(c.VoiceLock!=LongText.Signature(engine,o,c)||engine.Archive==null||LongText.Parts(c,o).Any(text=>!engine.Archive.Has(engine,text,o.BaseSpeed)))throw new Exception("Đoạn "+c.Id+" đã khóa nhưng thiết lập hoặc âm thanh đã thay đổi. Mở khóa đoạn trước khi tạo lại.");}
     if(o.RunReport!=null)o.RunReport.Step("TTS","Yêu cầu tổng hợp / lấy audio TTS");var paced=Punctuation.Generate(engine,c.Text,o.BaseSpeed,o,Path.Combine(work,"cue.wav"));if(o.RunReport!=null)o.RunReport.Step("ASSEMBLE","Ghép lời với nghỉ trong câu");Wave wav=paced.Join();if(o.RunReport!=null)o.RunReport.Natural(paced,wav.Duration);c.Natural=wav.Duration;c.Speed=o.BaseSpeed;c.AudioPath=null;
     double boundary=i<cues.Count-1?Punctuation.After(c.Text,o):0;double reserve=o.Locked&&i<cues.Count-1?Math.Min(Math.Max(0,c.Duration-.08),Math.Max(0,boundary-Math.Max(0,cues[i+1].Start-c.End))):0;
     if(o.Locked){
      if(o.RunReport!=null)o.RunReport.Step("FIT","Chỉnh nghỉ và căn tốc độ câu");wav=Punctuation.Fit(paced,c,o,engine.Root,work,()=>engine.Cancelled,ref reserve);
      if(wav.Duration>c.Duration-reserve){c.Status="QUÁ DÀI: +"+(wav.Duration-(c.Duration-reserve)).ToString("0.000")+"s tại tốc độ "+c.Speed.ToString("0.00")+" (tối đa "+Tempo.Maximum(o).ToString("0.00")+")";failed=true;if(o.RunReport!=null)o.RunReport.UnitDone(c,wav,false);continue;}
      c.Status=VoiceRhythm.Status(c,o)+(o.Punctuation==PunctuationMode.Custom?" · nghỉ dấu câu":"");
     }else{if(o.Prose&&previousCue!=null){cursor=Math.Max(Math.Max(previousCue.End,minimumCursor),cursor-Math.Min(Math.Max(0,cursor-previousCue.End),Punctuation.EdgeSilence(wav,true)));}c.Start=cursor;c.End=c.Start+wav.Duration;c.Actual=wav.Duration;c.Status="Timeline theo voice";}
     if(c.End>Scripts.MaxSeconds)throw new Exception("WAV dài hơn 6 giờ; hãy chia kịch bản.");
     if(o.RunReport!=null)o.RunReport.Step("ASSEMBLE","Ghi lời và hơi thở vào WAV trung gian");writer.At(c.Start,wav);
     double room=o.Locked&&i<cues.Count-1?cues[i+1].Start-c.Start-wav.Duration:Double.MaxValue;
     Wave breath=i<cues.Count-1?track.After(c,rate,room):null;
     if(breath!=null&&o.Locked&&Math.Round(c.Start*rate)+wav.Samples.Length+Math.Round(.04*rate)+breath.Samples.Length+Math.Ceiling(.04*rate)>Math.Round(cues[i+1].Start*rate))breath=null;
     minimumCursor=c.End;if(breath!=null){minimumCursor=c.Start+wav.Duration+.08+breath.Duration;track.Inserted(c);c.Status+=" · hơi thở";var scaled=new short[breath.Samples.Length];track.Mix(breath,wav,scaled,0);writer.At(c.Start+wav.Duration+.04,new Wave{Rate=rate,Samples=scaled});}
     if(!o.Locked){double desired=Math.Max(o.Gap,o.Prose&&c.ParagraphEnd?o.ParagraphPause:boundary);double added=o.Prose?Math.Max(0,desired-Punctuation.EdgeSilence(wav,false)):desired;cursor=c.End+Math.Max(added,breath==null?0:breath.Duration+.08);}
     if(o.RunReport!=null){double after=o.Locked?(i<cues.Count-1?Math.Max(0,cues[i+1].Start-c.Start-wav.Duration):Math.Max(0,c.End-c.Start-wav.Duration)):(i<cues.Count-1?Math.Max(0,cursor-c.End):0);o.RunReport.UnitDone(c,wav,true,after,breath==null?0:breath.Duration);}
     previousCue=c;
     if(meter!=null&&o.ReadingChanged!=null)o.ReadingChanged(meter.Update(c,c.End,"Đang đọc · "+c.Status));
    }
    if(failed){if(o.RunReport!=null)o.RunReport.Step("FIT_RESULT","Từ chối xuất do câu không vừa timeline");var bad=cues.Where(c=>c.Status.StartsWith("QUÁ")).ToList();throw new Exception("Có "+bad.Count+" câu vẫn vượt timeline sau khi tăng đến giới hạn. Chưa xuất WAV.\r\n"+String.Join("\r\n",bad.Take(12).Select(c=>"Câu "+c.Id+": "+c.Status))+"\r\nGiảm lời hoặc điều chỉnh giới hạn tốc độ / timeline.");}
    if(o.RunReport!=null)o.RunReport.Step("ASSEMBLE","Hoàn tất WAV trung gian");progress(95,"Hoàn tất WAV · kiểm tra độ dài");writer.Finish(cues.Last().End);
   }
   if(engine.Cancelled)throw new OperationCanceledException();
   if(meter!=null&&o.ReadingChanged!=null)o.ReadingChanged(meter.Snapshot("Đang ghép / cân âm lượng…"));
   if(o.RepairSnapshot!=null)o.RepairSnapshot(temporary);
   if(o.NormalizeLoudness){if(o.RunReport!=null)o.RunReport.Step("NORMALIZE","Đo và cân âm lượng WAV");string normalized=Path.Combine(work,"normalized.wav");FinalAudio.Normalize(engine.Root,temporary,normalized,(long)Math.Round(cues.Last().End*rate),o.TargetLufs,()=>engine.Cancelled,m=>progress(97,m));File.Copy(normalized,temporary,true);}
   if(engine.Cancelled)throw new OperationCanceledException();
   if(o.RunReport!=null)o.RunReport.Step("PUBLISH","Lưu WAV cuối cạnh nguồn");string final=OutputFiles.Publish(temporary,output);if(meter!=null&&o.ReadingChanged!=null)o.ReadingChanged(meter.Snapshot("WAV đã xuất",true));progress(100,"Hoàn tất: "+Path.GetFileName(final));return final;
  }finally{engine.RenderContext=previousContext;try{if(File.Exists(temporary))File.Delete(temporary);}catch{}try{if(Directory.Exists(work))Directory.Delete(work,true);}catch{}foreach(var c in cues)c.AudioPath=null;}
 }
}
}
