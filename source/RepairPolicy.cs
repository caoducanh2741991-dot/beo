using System;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
public sealed class SemanticCheck {public bool Safe,Rejected;public string Reason="";}
public static class RepairPolicy {
 public const int MaxAttempts=3;
 static readonly Dictionary<string,string> contractions=new Dictionary<string,string>{
  {"can't","can not"},{"cannot","can not"},{"won't","will not"},{"don't","do not"},{"doesn't","does not"},{"didn't","did not"},{"isn't","is not"},{"aren't","are not"},{"wasn't","was not"},{"weren't","were not"},{"hasn't","has not"},{"haven't","have not"},{"hadn't","had not"},{"wouldn't","would not"},{"shouldn't","should not"},{"couldn't","could not"},{"mustn't","must not"},{"we're","we are"},{"they're","they are"},{"you're","you are"},{"i'm","i am"},{"we've","we have"},{"they've","they have"},{"you've","you have"},{"i've","i have"},{"i'll","i will"},{"he'll","he will"},{"she'll","she will"},{"we'll","we will"},{"they'll","they will"},{"you'll","you will"}};
 // These words still require human approval when changed. They are not a
 // license to drop actions, modal verbs, negation, quantities or causal links.

 public static string Canonical(string text){string t=(text??"").Replace('’','\'').ToLowerInvariant();foreach(var x in contractions.OrderByDescending(x=>x.Key.Length))t=Regex.Replace(t,@"\b"+Regex.Escape(x.Key)+@"\b",x.Value);return String.Join(" ",Regex.Matches(t,@"[\p{L}\p{N}]+(?:'[\p{L}]+)?").Cast<Match>().Select(m=>m.Value));}
 static string PunctuationKey(string text){string t=(text??"").Replace('’','\'').ToLowerInvariant();foreach(var x in contractions.OrderByDescending(x=>x.Key.Length))t=Regex.Replace(t,@"\b"+Regex.Escape(x.Key)+@"\b",x.Value);return Regex.Replace(t,@"\s+"," ").Trim();}
 public static string Signature(IEnumerable<Cue> cues){return String.Join("|",cues.Select(c=>c.Id+":"+Canonical(c.Text)));}
 static readonly HashSet<string> ordinaryNames=new HashSet<string>("The This That These Those He She They We You It I A An And But So First Then After Before Nobody Everyone Instead Could Would Is Was His Her Their Our What When Why How Even Still Soon Almost Against Days Not Once Suddenly".Split(' '));
 static readonly HashSet<string> numberWords=new HashSet<string>("zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty thirty forty fifty sixty seventy eighty ninety hundred thousand million billion dollars dollar pounds euros percent".Split(' '));
 static readonly Dictionary<string,string> equivalents=new Dictionary<string,string>{
  {"enormous","huge"},{"massive","huge"},{"entirely","completely"},{"beginning","start"},{"headed","go"},{"went","go"},{"traveled","go"},{"seemed","look"},{"bought","buy"},{"buying","buy"},{"purchased","buy"},{"carried","carry"},{"carrying","carry"},{"ignored","ignore"},{"ignoring","ignore"},{"knew","know"},{"realized","realize"},{"weighed","weigh"},{"opened","open"},{"watched","watch"},{"looked","look"},{"appeared","appear"},{"glowed","glow"},{"glowing","glow"},{"flew","fly"},{"vanished","vanish"},{"exploded","explode"},{"added","add"},{"removed","remove"},{"discovered","discover"},{"found","find"},{"inspected","inspect"},{"tasted","taste"},{"counted","count"}};
 static string MeaningTokens(string text){return String.Join(" ",Canonical(text).Split(' ').Select(w=>equivalents.ContainsKey(w)?equivalents[w]:w));}
 static string SafeSurface(string text){string value=PunctuationKey(text);foreach(var pair in new[]{new[]{"enormous","huge"},new[]{"massive","huge"},new[]{"entirely","completely"},new[]{"beginning","start"},new[]{"headed","went"}})value=Regex.Replace(value,@"\b"+pair[0]+@"\b",pair[1]);return value;}
 static readonly HashSet<string> actions=new HashSet<string>("go buy carry ignore know realize weigh open watch look appear glow fly vanish explode add remove discover find inspect taste count sell kill die burn steal leave arrive lift cut cook eat drink".Split(' '));
 public static SemanticCheck Check(List<Cue> before,List<Cue> after){
  ValidateGeometry(before,after);var result=new SemanticCheck();
  for(int i=0;i<before.Count;i++){
   string a=Canonical(before[i].Text),b=Canonical(after[i].Text);var aa=a.Split(' ');var bb=b.Split(' ');
   var numbers=new Regex(@"(?:[-+]?[$€£]?|[$€£][-+]?)\d[\d,.]*(?:%)?");
   if(!numbers.Matches(before[i].Text).Cast<Match>().Select(m=>m.Value).SequenceEqual(numbers.Matches(after[i].Text).Cast<Match>().Select(m=>m.Value))||!aa.Where(numberWords.Contains).SequenceEqual(bb.Where(numberWords.Contains)))return new SemanticCheck{Rejected=true,Reason="ENTITY_CHANGED: số/số tiền hoặc số viết bằng chữ khác tại cue #"+before[i].Id};
   Func<string,IEnumerable<string>> names=s=>Regex.Matches(s,@"\b[A-Z][a-z]+(?:[-'][A-Z]?[a-z]+)*\b").Cast<Match>().Select(m=>m.Value).Where(w=>!ordinaryNames.Contains(w));
   if(!names(before[i].Text).SequenceEqual(names(after[i].Text)))return new SemanticCheck{Rejected=true,Reason="ENTITY_CHANGED: tên riêng/địa điểm/từ viết hoa khác tại cue #"+before[i].Id};
   var neg=new HashSet<string>("not never nobody nothing neither without".Split(' '));if(!aa.Where(neg.Contains).SequenceEqual(bb.Where(neg.Contains)))return new SemanticCheck{Rejected=true,Reason="FACT_CHANGED: phủ định khác tại cue #"+before[i].Id};
   string ma=MeaningTokens(before[i].Text),mb=MeaningTokens(after[i].Text);
   var actA=ma.Split(' ').Where(actions.Contains).ToList();var actB=mb.Split(' ').Where(actions.Contains).ToList();
   if(actB.Except(actA).Any()||(actA.OrderBy(x=>x).SequenceEqual(actB.OrderBy(x=>x))&&!actA.SequenceEqual(actB)))return new SemanticCheck{Rejected=true,Reason="FACT_CHANGED: hành động mới hoặc thứ tự hành động khác tại cue #"+before[i].Id};
   if(!actA.SequenceEqual(actB))result.Reason="SEMANTIC_REVIEW_REQUIRED: động từ/cấu trúc hành động khác; cần đối chiếu nghĩa thay vì khóa từ nguyên văn.";
   if(a==b){if(PunctuationKey(before[i].Text)!=PunctuationKey(after[i].Text))result.Reason="SEMANTIC_REVIEW_REQUIRED: dấu câu có thể đổi phạm vi ý nghĩa.";continue;}
   if(ma==mb){if(SafeSurface(before[i].Text)!=SafeSurface(after[i].Text))result.Reason="SEMANTIC_REVIEW_REQUIRED: đổi dạng động từ, dấu câu hoặc cấu trúc; không tự coi là đồng nghĩa chỉ vì stem trùng.";continue;}
   result.Reason="SEMANTIC_REVIEW_REQUIRED: cách diễn đạt thay đổi; đối chiếu sự kiện, kết quả, sắc thái và nhân quả trước khi chấp nhận.";
  }
  result.Safe=result.Reason.Length==0;if(result.Safe)result.Reason="NORMALIZED_EQUIVALENT: cấu trúc/từ đồng nghĩa hẹp và thực thể giữ nguyên; không phải chứng minh ngữ nghĩa tổng quát.";return result;
 }
 public static bool Acceptable(AiAttempt a){return a!=null&&!a.Rejected&&a.Measurement!=null&&a.Measurement.Valid&&!a.Measurement.NeedsRepair;}
 public static void ValidateGeometry(List<Cue> original,List<Cue> proposed){
  if(original.Count!=proposed.Count||original.Count==0)throw new Exception("Sai số cue.");
  for(int i=0;i<original.Count;i++){var a=original[i];var b=proposed[i];if(a.Id!=b.Id||a.Start!=b.Start||a.End!=b.End||a.Speaker!=b.Speaker||a.AnchorAfter!=b.AnchorAfter)throw new Exception("Đề xuất đổi ID, timecode, neo hoặc giọng A/B.");if(String.IsNullOrWhiteSpace(b.Text)||b.Text.Length>2000||b.Text.Contains("-->")||Regex.IsMatch(b.Text,@"\[[a-zA-Z]\]|<\||```|^\s*(?:[AB]:|\d+:\d{2}:)"))throw new Exception("Lời đề xuất chứa nhãn hoặc nội dung không hợp lệ.");}
 }
 public static string Quality(double speed){return speed>1.10001?"CẢNH BÁO CHẤT LƯỢNG: vượt 1.10x; đạt kỹ thuật không đồng nghĩa giọng tự nhiên.":"Trong vùng ưu tiên 0.90–1.10x.";}
}
public sealed class RepairPendingException:Exception {public readonly AiRepairSession Session;public RepairPendingException(AiRepairSession p):base("CẦN SỬA THỦ CÔNG hoặc duyệt lời: mở SỬA NGAY tại chặng lỗi rồi TIẾP TỤC RENDER."){Session=p;}}
public sealed class BreathState {
 public int Sentences,Index;public double Last=-100;
 public string Key{get{return Sentences+"|"+Index+"|"+Last.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}}
 public BreathState Copy(){return new BreathState{Sentences=Sentences,Index=Index,Last=Last};}
}
}
