using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
// Speaker identity is independent of VoiceLock (a cached TXT audio lock).
public static class SpeakerScript {
 public static string Role(Cue c){return c.Speaker=="B"?"B":"A";}
 public static string Tagged(Cue c){return String.IsNullOrEmpty(c.Speaker)?c.Text:"["+c.Speaker+"] "+c.Text;}
 public static void Extract(Cue c){
  string s=(c.Text??"").Trim();var m=Regex.Match(s,@"^\[([AB])\]\s+(.+)$",RegexOptions.Singleline);
  if(m.Success){if(!String.IsNullOrEmpty(c.Speaker)&&c.Speaker!=m.Groups[1].Value)throw new Exception("Câu "+c.Id+": nhãn giọng khác cột Giọng. Sửa cột Giọng hoặc bỏ nhãn trùng.");c.Speaker=m.Groups[1].Value;s=m.Groups[2].Value.Trim();}
  if(Regex.IsMatch(s,@"\[[a-zA-Z]\]|^\[\s*[aAbB](?:\s|\]|$)|^[ABab]\s*:")||s=="[A]"||s=="[B]")throw new Exception("Câu "+c.Id+": nhãn giọng không hợp lệ. Dùng [A] lời hoặc [B] lời, một giọng cho mỗi câu.");
  if(c.Speaker!=""&&c.Speaker!="A"&&c.Speaker!="B")throw new Exception("Câu "+c.Id+": chỉ nhận giọng A hoặc B.");
  c.Text=s;
 }
 public static void Imported(List<Cue> cues){foreach(var c in cues)Extract(c);if(cues.Any(c=>c.Speaker!="")&&cues.Any(c=>c.Speaker==""))throw new Exception("Kịch bản hai giọng còn câu thiếu [A] / [B]. Gắn nhãn vào đầu mọi câu/lượt đọc trước khi nhập.");}
 public static bool HasTags(string text){return Regex.IsMatch(text??"",@"(?m)^\s*(?:\[\s*[a-zA-Z](?:\]|\s|$)|[ABab]\s*:)");}
 public static List<Cue> ParseProse(string text){var all=new List<Cue>();double t=0;foreach(string line in text.Replace("\r","").Split('\n')){if(String.IsNullOrWhiteSpace(line))continue;var head=new Cue{Id=all.Count+1,Text=line};Extract(head);if(head.Speaker=="")throw new Exception("TXT hai giọng: mỗi lượt trên một dòng, bắt đầu bằng [A] hoặc [B].");foreach(var c in LongText.ParsePlain(head.Text)){c.Speaker=head.Speaker;c.Id=all.Count+1;double duration=c.Duration;c.Start=t;c.End=t+duration;t=c.End+.15;all.Add(c);}}Scripts.Validate(all);return all;}
 public static void ValidateRun(List<Cue> cues,bool two){foreach(var c in cues)Extract(c);if(!two&&cues.Any(c=>Role(c)=="B"))throw new Exception("Kịch bản có giọng B. Bật 2 giọng và chọn A/B trước khi tạo voice.");}
}
// Route before punctuation splitting, cache lookup and fitting. Workers are owned
// by this render, and heavyweight models are released when changing speakers.
public sealed class TwoVoiceEngine:KokoroEngine {
 readonly KokoroEngine a,b;readonly string keyA,keyB;KokoroEngine current;string role="A";HashSet<string> used=new HashSet<string>();bool disposed;
 public TwoVoiceEngine(KokoroEngine first,KokoroEngine second,string firstKey,string secondKey,IEnumerable<Cue> cues){a=first;b=second;keyA=firstKey;keyB=secondKey;Root=a.Root;DeviceMode=a.DeviceMode;PreviewCache=a.PreviewCache;Archive=a.Archive;DeviceChanged=a.DeviceChanged;ActivityChanged=a.ActivityChanged;foreach(var c in cues)used.Add(SpeakerScript.Role(c));Mirror(a);}
 static bool Heavy(KokoroEngine e){return e.Kind==EngineKind.Qwen||e.Kind==EngineKind.Chatterbox||e.Kind==EngineKind.VieNeu||e.Kind==EngineKind.Rvc;}
 void Mirror(KokoroEngine e){Kind=e.Kind;Speaker=e.Speaker;ModelFolder=e.ModelFolder;VoiceName=e.VoiceName;ReferencePath=e.ReferencePath;ReferenceText=e.ReferenceText;Threads=e.Threads;}
 public override void SelectSpeaker(string speaker){if(Cancelled)throw new OperationCanceledException();role=speaker=="B"?"B":"A";var next=role=="B"?b:a;if(current!=null&&current!=next&&Heavy(current))current.Dispose();current=next;Mirror(current);if(RunReport!=null)RunReport.SelectVoice(role,role=="B"?keyB:keyA,current);}
 public override void CheckFiles(){if(used.Contains("A"))a.CheckFiles();if(used.Contains("B"))b.CheckFiles();}
 public override Wave GetSpeech(string text,double speed,string output){if(Cancelled)throw new OperationCanceledException();if(current==null)SelectSpeaker("A");current.Archive=Archive;current.PreviewCache=PreviewCache;current.RunReport=RunReport;current.RenderContext=RenderContext;current.DeviceChanged=x=>ReportDevice("Giọng "+role+" · "+current.VoiceName+": "+x);current.ActivityChanged=ReportActivity;return current.GetSpeech(text,speed,output);}
 public override Wave Generate(string text,double speed,string output){return GetSpeech(text,speed,output);}
 public override void Cancel(){base.Cancel();a.Cancel();b.Cancel();}
 public override void Dispose(){if(disposed)return;disposed=true;a.Dispose();b.Dispose();base.Dispose();}
}
}
