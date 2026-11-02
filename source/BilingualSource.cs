using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
public sealed class BilingualSource {
 public string FileName="";public Dictionary<int,string> Text=new Dictionary<int,string>();
 static string Normalize(string text){return Regex.Replace(text??"",@"\s+"," ").Trim();}
 public static BilingualSource Read(string source,List<Cue> cues){var result=new BilingualSource();if(String.IsNullOrEmpty(source))return result;
  string stem=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)),Path.GetFileNameWithoutExtension(source));
  foreach(string file in new[]{stem+"_Tieng_Viet.txt",stem+".vi.txt"}){if(!System.IO.File.Exists(file)||new FileInfo(file).Length>10*1024*1024)continue;
   string data=System.IO.File.ReadAllText(file);var blocks=Regex.Matches(data,@"(?ms)^\s*#?(\d+)\s*\|\s*(\d{2}:\d{2}:\d{2},\d{3})\s*(?:-->|→)\s*(\d{2}:\d{2}:\d{2},\d{3})[^\r\n]*\r?\nEN:\s*(.*?)\r?\nVI:\s*(.*?)(?=^\s*#?\d+\s*\||\z)");
   var found=new Dictionary<int,string>();foreach(Match b in blocks){int id;if(!Int32.TryParse(b.Groups[1].Value,out id)||found.ContainsKey(id))continue;var cue=cues.FirstOrDefault(c=>c.Id==id);if(cue==null||b.Groups[2].Value!=Scripts.Stamp(cue.Start)||b.Groups[3].Value!=Scripts.Stamp(cue.End)||Normalize(b.Groups[4].Value)!=Normalize(cue.Text))continue;
    string translated=b.Groups[5].Value.Trim();try{OfflineTranslation.Validate(cue.Text,translated);found[id]=translated;}catch{/* Mismatched/placeholder/numeric-invalid translations are not reused. */}
   }
   if(found.Count>0){result.FileName=file;result.Text=found;return result;}
  }return result;
 }
}
}
