using System;using System.IO;using System.Linq;using System.Text;using System.Text.RegularExpressions;using System.Collections.Generic;using System.Globalization;using System.Security.Cryptography;using System.Diagnostics;
namespace V15Voice {
public static class LongText {
 static readonly Regex Abbrev=new Regex(@"(?:\b(?:Mr|Mrs|Ms|Dr|Prof|Sr|Jr|St|vs|etc|Fig|No|U\.S|U\.K|e\.g|i\.e)|\b[A-Za-z])\.$",RegexOptions.IgnoreCase);
 public static List<Cue> Parse(string text){return SpeakerScript.HasTags(text)?SpeakerScript.ParseProse(text):ParsePlain(text);}
 internal static List<Cue> ParsePlain(string text){
  var result=new List<Cue>();double time=0;foreach(var paragraph in Regex.Split(text.Trim('\uFEFF',' ','\r','\n','\t').Replace("\r\n","\n").Replace("\r","\n"),@"\n\s*\n")){
   string value=Regex.Replace(paragraph.Trim(),@"\s+"," ");if(value.Length==0)continue;var sentences=new List<string>();int start=0;
   for(int i=0;i<value.Length;i++){char c=value[i];if(".!?".IndexOf(c)<0)continue;if(c=='.'&&((i>0&&i+1<value.Length&&Char.IsDigit(value[i-1])&&Char.IsDigit(value[i+1]))||Abbrev.IsMatch(value.Substring(start,i+1-start))))continue;int end=i+1;while(end<value.Length&&".!?\"”’')]}".IndexOf(value[end])>=0)end++;if(end<value.Length&&!Char.IsWhiteSpace(value[end]))continue;sentences.Add(value.Substring(start,end-start).Trim());start=end;i=end-1;}
   if(start<value.Length)sentences.Add(value.Substring(start).Trim());
   foreach(string sentence in sentences.Where(x=>x.Length>0)){
    // Keep whole sentences when possible; bounded clauses for abnormally long sentences.
    string pending=sentence;while(pending.Length>900){int cut=pending.LastIndexOfAny(new[]{',',';',':','—'},Math.Min(850,pending.Length-1),Math.Min(600,pending.Length-250));if(cut<250)cut=pending.LastIndexOf(' ',850);if(cut<1)cut=850;else cut++;Add(result,pending.Substring(0,cut).Trim(),ref time);pending=pending.Substring(cut).TrimStart();}if(pending.Length>0)Add(result,pending,ref time);
   }if(result.Count>0)result.Last().ParagraphEnd=true;
  }Scripts.Validate(result);return result;
 }
 static void Add(List<Cue> list,string text,ref double time){var c=new Cue{Id=list.Count+1,Text=text,Prose=true,Start=time};c.End=time+Math.Max(.8,c.Words/2.5);list.Add(c);time=c.End+.15;}
 public static void Validate(List<Cue> cues){double t=0;var draft=new List<Cue>();for(int i=0;i<cues.Count;i++){var c=cues[i];c.Id=i+1;double seconds=Math.Max(.08,c.Actual>0?c.Actual:Math.Max(.8,c.Words/2.5));draft.Add(new Cue{Id=c.Id,Start=t,End=t+seconds,Text=c.Text});t+=seconds+.15;}Scripts.Validate(draft);}
 public static string Signature(KokoroEngine e,RenderOptions o,Cue c){return SpeechArchive.Hash(SpeechArchive.Key(e,c.Text,o.BaseSpeed)+"|"+o.Punctuation+"|"+o.CommaPause.ToString("R",CultureInfo.InvariantCulture)+"|"+o.StopPause.ToString("R",CultureInfo.InvariantCulture)+"|"+o.QuestionPause.ToString("R",CultureInfo.InvariantCulture)+"|"+o.ParagraphPause.ToString("R",CultureInfo.InvariantCulture));}
 public static List<string> Parts(Cue c,RenderOptions o){return o.Punctuation==PunctuationMode.Auto?new List<string>{c.Text}:Punctuation.Split(c.Text,o).Select(p=>p.Text).ToList();}
 public static double Boundary(Cue c,Cue next,Wave current,Wave following,RenderOptions o){double desired=Math.Max(o.Gap,c.ParagraphEnd?o.ParagraphPause:Punctuation.After(c.Text,o));return Math.Max(0,desired-Punctuation.EdgeSilence(current,false)-(following==null?0:Punctuation.EdgeSilence(following,true)));}
}
public sealed class ReadingProgress {
 public int Done,Total,Words;public double AudioSeconds,EstimateLow,EstimateHigh,RemainingWork=-1;public bool Final;public string Stage;
 public string Summary {get{return (Final?"WAV hoàn tất: "+Scripts.Stamp(AudioSeconds):"Voice đã tạo: "+Scripts.Stamp(AudioSeconds))+"\r\n"+(Final?"Thời lượng chính xác":"Voice dự kiến: "+Scripts.Stamp(EstimateLow)+" – "+Scripts.Stamp(EstimateHigh))+"\r\n"+Done+" / "+Total+" đoạn · "+Words+" từ\r\nMáy còn xử lý: "+(Final?"Đã xong":Done==Total?"Đang hoàn thiện WAV…":RemainingWork<0?"Đang tính…":"khoảng "+Scripts.Stamp(RemainingWork).Split(',')[0])+"\r\n"+(Stage??"");}}
}
public sealed class ReadingMeter {
 readonly int total,words;readonly double speed;readonly Dictionary<string,int> roleWords=new Dictionary<string,int>(),roleDone=new Dictionary<string,int>();readonly Dictionary<string,double> roleAudio=new Dictionary<string,double>();readonly Stopwatch timer=Stopwatch.StartNew();int done,doneWords,firstWords;double audio,voice,firstTime;
 public ReadingMeter(List<Cue> cues,double speed){total=cues.Count;words=cues.Sum(c=>c.Words);this.speed=speed;foreach(var g in cues.GroupBy(SpeakerScript.Role)){roleWords[g.Key]=g.Sum(c=>c.Words);roleDone[g.Key]=0;roleAudio[g.Key]=0;}}
 public ReadingProgress Update(Cue c,double end,string stage){done++;doneWords+=c.Words;voice+=c.Actual;string role=SpeakerScript.Role(c);roleDone[role]+=c.Words;roleAudio[role]+=c.Actual;audio=end;if(done==1){firstTime=timer.Elapsed.TotalSeconds;firstWords=doneWords;}return Snapshot(stage);}
 public ReadingProgress Snapshot(string stage,bool final=false){double remaining=roleWords.Sum(p=>Math.Max(0,p.Value-roleDone[p.Key])*(roleDone[p.Key]>0?roleAudio[p.Key]/roleDone[p.Key]:.4/speed));double uncertainty=done<3?.25:.12;return new ReadingProgress{Done=done,Total=total,Words=words,AudioSeconds=audio,EstimateLow=audio+remaining*(1-uncertainty),EstimateHigh=audio+remaining*(1+uncertainty)+Math.Max(0,total-done)*.3,RemainingWork=doneWords>firstWords?(timer.Elapsed.TotalSeconds-firstTime)/(doneWords-firstWords)*Math.Max(0,words-doneWords):-1,Final=final,Stage=stage};}
}
// Persistent bounded waveform store. No model weights or GPU buffers are retained here.
public sealed class SpeechArchive {
 readonly string folder;public bool Refresh;
 public SpeechArchive(string state){folder=Path.Combine(state,"text_audio");Directory.CreateDirectory(folder);Prune();}
 public static string Hash(string text){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
 static string Stamp(string path){if(String.IsNullOrEmpty(path)||!File.Exists(path))return "";var f=new FileInfo(path);return f.Length+":"+f.LastWriteTimeUtc.Ticks;}
 public static string ModelStamp(KokoroEngine e){if(e.Kind!=EngineKind.Qwen&&e.Kind!=EngineKind.Chatterbox&&e.Kind!=EngineKind.VieNeu)return Stamp(e.ModelFile);string folder=Path.Combine(e.Root,"ai","models",e.ModelFolder??"");var files=Directory.Exists(folder)?Directory.GetFiles(folder,"*",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal).Select(x=>x.Substring(folder.Length)+":"+Stamp(x)).ToList():new List<string>();string worker=Path.Combine(e.Root,"ai","worker.py");if(File.Exists(worker))files.Add("worker:"+StateFiles.Hash(worker));if(e.Kind==EngineKind.Qwen){string tokenizer=Path.Combine(e.Root,"ai","models","qwen_custom","speech_tokenizer","model.safetensors");files.Add("tokenizer:"+Stamp(tokenizer));}return Hash(String.Join("|",files));}
 public static string Key(KokoroEngine e,string text,double speed){string reference=String.IsNullOrEmpty(e.ReferencePath)||!File.Exists(e.ReferencePath)?"":StateFiles.Hash(e.ReferencePath);return Hash(String.Join("\u001f",new[]{"V15-text-1.4.6",e.Root,e.Kind.ToString(),e.ModelFolder,e.VoiceName,e.Speaker.ToString(),reference,e.ReferenceText,e.DeviceMode,ModelStamp(e),speed.ToString("R",CultureInfo.InvariantCulture),e.Kind==EngineKind.Rvc?text+"\u001e"+CommunityVoices.Fingerprint(e):text}));}
 string PathFor(KokoroEngine e,string text,double speed){return Path.Combine(folder,Key(e,text,speed)+".wav");}
 public Wave Get(KokoroEngine e,string text,double speed){if(Refresh)return null;string path=PathFor(e,text,speed);if(!File.Exists(path))return null;try{var wave=Wave.Read(path);if(wave.Rate!=24000||wave.Samples.Length==0)return null;File.SetLastWriteTimeUtc(path,DateTime.UtcNow);e.ReportActivity("Dùng đoạn TXT đã lưu");return wave;}catch{return null;}}
 public void Put(KokoroEngine e,string text,double speed,Wave wave){string path=PathFor(e,text,speed),tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{wave.Resample(24000).Write(tmp);if(File.Exists(path))File.Replace(tmp,path,path+".previous");else File.Move(tmp,path);}finally{try{File.Delete(tmp);}catch{}}}
 public bool Has(KokoroEngine e,string text,double speed){try{string path=PathFor(e,text,speed);return File.Exists(path)&&Wave.Read(path).Samples.Length>0;}catch{return false;}}
 public bool CanRestore(KokoroEngine e,string text,double speed){return File.Exists(PathFor(e,text,speed)+".previous");}
 public bool Restore(KokoroEngine e,string text,double speed){string path=PathFor(e,text,speed),previous=path+".previous";if(!File.Exists(previous))return false;Wave.Read(previous);string swap=path+".swap";File.Copy(path,swap,true);File.Copy(previous,path,true);File.Copy(swap,previous,true);File.Delete(swap);return true;}
 void Prune(){var files=new DirectoryInfo(folder).GetFiles().Where(f=>f.Name.EndsWith(".wav")||f.Name.EndsWith(".previous")).OrderBy(f=>f.LastWriteTimeUtc).ToList();long size=files.Sum(f=>f.Length);foreach(var f in files){if(size<1024L*1024*1024)break;try{f.Delete();size-=f.Length;}catch{}}}
}
}
