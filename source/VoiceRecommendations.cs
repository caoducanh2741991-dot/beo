using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
public sealed class VoiceSuggestion {public VoiceProfile Profile;public string Reason="";public bool Available;public int Rank;}
public sealed class VoiceAdvice {public string Style="Kể chuyện",Language="English",Note="";public List<VoiceSuggestion> Suggestions=new List<VoiceSuggestion>();}
public static class VoiceRecommendations {
 static int Hits(string text,string words){return words.Split('|').Count(w=>Regex.IsMatch(text,@"\b"+Regex.Escape(w)+@"\b",RegexOptions.IgnoreCase));}
 public static VoiceAdvice Analyze(IEnumerable<Cue> source,IEnumerable<VoiceProfile> profiles,string preferred,Func<VoiceProfile,bool> installed){
  var result=new VoiceAdvice();string text=String.Join(" ",source.Take(40).Select(c=>c.Text));if(text.Length>12000)text=text.Substring(0,12000);if(String.IsNullOrWhiteSpace(text)){result.Note="Nhập SRT/TXT để xem gợi ý giọng.";return result;}
  bool vi=Hits(text,"xin|chào|tôi|bạn|không|một|được|người|của|và|đã|đang")>=2||Regex.Matches(text,@"[ăâđêôơưĂÂĐÊÔƠƯạảấầẩẫậắằẳẵặẹẻẽếềểễệịỉĩọỏốồổỗộớờởỡợụủũứừửữựỳỷỹ]",RegexOptions.IgnoreCase).Count>=4;result.Language=vi?"Tiếng Việt":"English";
  int dark=Hits(text,"shadow|haunted|creepy|whisper|eerie|mysterious|strange|unexplained|terrifying|horror"),mind=Hits(text,"psychology|brain|dopamine|memory|attention|anxiety|cognitive|habits|psychological"),guide=Hits(text,"step|instructions|tutorial|recipe|ingredients|install|method"),review=Hits(text,"review|tested|rating|product|price|recommend");
  if(dark>=2)result.Style="Bí ẩn / Dark curiosity";else if(mind>=2)result.Style="Psychology Facts / V15";else if(guide>=3)result.Style="Giải thích / Hướng dẫn";else if(review>=3)result.Style="Review";
  var ranked=profiles.Where(p=>!String.IsNullOrEmpty(p.Key)&&((p.Kind==EngineKind.VieNeu)==vi)).Select(p=>new{Profile=p,Score=(p.MatchesStyle(result.Style)?40:0)+(p.MatchesStyle("Kể chuyện")?8:0)+(p.Key==preferred?12:0)+(p.Recommended?5:0)+(p.Kind==EngineKind.Kokoro&&!vi?3:0)}).OrderByDescending(p=>p.Score).ThenBy(p=>p.Profile.Order).ToList();
  // No synthesis/model load or LLM call. Readiness is checked by installed files only.
  var ready=new Dictionary<string,bool>();var candidates=new List<VoiceSuggestion>();foreach(var item in ranked){var p=item.Profile;string key=p.Kind+"|"+p.ModelFolder+"|"+p.ReferencePath;bool ok;if(!ready.TryGetValue(key,out ok)){try{ok=installed(p);}catch{ok=false;}ready[key]=ok;}string reason=p.MatchesStyle(result.Style)?"Metadata giọng có phong cách "+result.Style:"Để nghe đối chiếu với phong cách "+result.Style;if(p.Key==preferred)reason+=" · giọng anh đang chọn";if(!ok)reason+=" · chưa đủ bộ giọng trên máy";candidates.Add(new VoiceSuggestion{Profile=p,Available=ok,Reason=reason,Rank=item.Score});}
  result.Suggestions=candidates.OrderByDescending(p=>p.Available).ThenByDescending(p=>p.Rank).ThenBy(p=>p.Profile.Order).Take(3).ToList();result.Note=result.Language+" · "+result.Style+" · gợi ý theo metadata, chưa đo TTS";return result;
 }
}
}
