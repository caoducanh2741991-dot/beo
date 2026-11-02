using System;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
public sealed class SemanticCheck {public bool Safe,Rejected;public string Reason="";}
public static class RepairPolicy {
 public const int MaxAttempts=3;
 static readonly Dictionary<string,string> contractions=new Dictionary<string,string>{
  {"can't","can not"},{"cannot","can not"},{"won't","will not"},{"don't","do not"},{"doesn't","does not"},{"didn't","did not"},{"isn't","is not"},{"aren't","are not"},{"wasn't","was not"},{"weren't","were not"},{"hasn't","has not"},{"haven't","have not"},{"hadn't","had not"},{"wouldn't","would not"},{"shouldn't","should not"},{"couldn't","could not"},{"mustn't","must not"},{"we're","we are"},{"they're","they are"},{"you're","you are"},{"i'm","i am"},{"we've","we have"},{"they've","they have"},{"you've","you have"},{"i've","i have"},{"i'll","i will"},{"he'll","he will"},{"she'll","she will"},{"we'll","we will"},{"they'll","they will"},{"you'll","you will"}};
 // These words still require human approval when changed. They are not a
 // license to drop actions, modal verbs, negation, quantities or causal links.
 static readonly HashSet<string> reviewWords=new HashSet<string>("a an the very really quite actually simply basically just".Split(' '));
 public static string Canonical(string text){string t=(text??"").Replace('’','\'').ToLowerInvariant();foreach(var x in contractions.OrderByDescending(x=>x.Key.Length))t=Regex.Replace(t,@"\b"+Regex.Escape(x.Key)+@"\b",x.Value);return String.Join(" ",Regex.Matches(t,@"[\p{L}\p{N}]+(?:'[\p{L}]+)?").Cast<Match>().Select(m=>m.Value));}
 static string PunctuationKey(string text){string t=(text??"").Replace('’','\'').ToLowerInvariant();foreach(var x in contractions.OrderByDescending(x=>x.Key.Length))t=Regex.Replace(t,@"\b"+Regex.Escape(x.Key)+@"\b",x.Value);return Regex.Replace(t,@"\s+"," ").Trim();}
 public static string Signature(IEnumerable<Cue> cues){return String.Join("|",cues.Select(c=>c.Id+":"+Canonical(c.Text)));}
 public static SemanticCheck Check(List<Cue> before,List<Cue> after){
  var result=new SemanticCheck();
  for(int i=0;i<before.Count;i++){
   string a=Canonical(before[i].Text),b=Canonical(after[i].Text);
   var numbers=new Regex(@"(?:[-+]?[$€£]?|[$€£][-+]?)\d[\d,.]*(?:%)?");if(!numbers.Matches(before[i].Text).Cast<Match>().Select(m=>m.Value).SequenceEqual(numbers.Matches(after[i].Text).Cast<Match>().Select(m=>m.Value)))return new SemanticCheck{Rejected=true,Reason="Khóa số liệu/tiền/đơn vị: không đổi dấu, số hoặc ký hiệu tiền tệ."};
   var ordinary=new HashSet<string>("The This That These Those He She They We You It A An And But So First Then After Before Nobody Everyone Instead Could Would Is Was His Her Their Our".Split(' '));foreach(Match name in Regex.Matches(before[i].Text,@"\b[A-Z][a-z]+\b"))if(!ordinary.Contains(name.Value)&&!Regex.IsMatch(after[i].Text,@"\b"+Regex.Escape(name.Value)+@"\b"))return new SemanticCheck{Rejected=true,Reason="Giữ nguyên tên/từ viết hoa: "+name.Value};
   if(a==b){if(PunctuationKey(before[i].Text)!=PunctuationKey(after[i].Text))result.Reason="Cần duyệt thay đổi dấu câu có thể đổi phạm vi ý nghĩa.";continue;}
   var aa=a.Split(' ');var bb=b.Split(' ');
   var sourceCore=aa.Where(w=>!reviewWords.Contains(w));var targetCore=bb.Where(w=>!reviewWords.Contains(w));
   if(!sourceCore.SequenceEqual(targetCore))return new SemanticCheck{Rejected=true,Reason="Khóa ngữ nghĩa: thêm/bỏ/đổi từ nội dung, tên, số lượng, phủ định hoặc thứ tự hành động tại cue #"+before[i].Id+". Cần sửa thủ công; không tự nhận tương đương nghĩa."};
   result.Reason="Cần duyệt thay đổi từ nhấn mạnh hoặc từ chức năng; chưa chứng minh giữ nguyên sắc thái.";
  }
  result.Safe=result.Reason.Length==0;
  if(result.Safe)result.Reason="Chỉ thay cách viết tắt/mở rộng không mơ hồ, chữ hoa hoặc dấu câu; chuỗi từ chuẩn hóa giữ nguyên.";
  return result;
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
