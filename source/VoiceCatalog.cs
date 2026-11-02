using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace V15Voice {
public static class VoiceStyles {
 public static readonly string[] Names={"Tất cả phong cách","Review","Kể chuyện","Bí ẩn / Dark curiosity","Psychology Facts / V15","Giải thích / Hướng dẫn","Quảng cáo / Hook TikTok","Tài liệu / Thuyết minh"};
}
public sealed class VoiceProfile {
 public string Key,Gender,Description,Source,Grade="Chưa có",Name="",ModelFolder="",Styles="",StyleReason="",Sample="",StarSource="";public bool Recommended;public int Speaker,Order;
 public string ReferencePath="",ReferenceText="";
 public EngineKind Kind;
 public override string ToString(){return Name;}
 public string MainStyle {get{return Styles.Split(new char[]{'|'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"Chưa phân loại";}}
 public bool MatchesStyle(string style){return String.IsNullOrEmpty(style)||Styles.Split(new char[]{'|'},StringSplitOptions.RemoveEmptyEntries).Contains(style);}
}
public sealed class VoiceCatalog {
 readonly Dictionary<string,VoiceProfile> profiles=new Dictionary<string,VoiceProfile>();
 readonly Dictionary<string,string> notes=new Dictionary<string,string>();readonly string notePath;
 public VoiceCatalog(string path,string userNotes){
  notePath=userNotes;
  if(File.Exists(path))foreach(string line in File.ReadAllLines(path)){
   var cells=line.Split(new char[]{'\t'},StringSplitOptions.None);if(cells.Length<4||cells[0]=="Key")continue;
   int sid;int.TryParse(cells[0].Substring(2),out sid);EngineKind kind=cells[0].StartsWith("L:")?EngineKind.LibriTTS:EngineKind.Kokoro;
   if(cells.Length>7)Enum.TryParse<EngineKind>(cells[7],out kind);
   if(cells.Length>8)int.TryParse(cells[8],out sid);
   profiles[cells[0]]=new VoiceProfile{Key=cells[0],Gender=cells[1],Description=cells[2],Source=cells[3],Grade=cells.Length>4?cells[4]:"Chưa có",Recommended=cells.Length>5&&cells[5]=="1",Name=cells.Length>6?cells[6]:cells[0],Kind=kind,Speaker=sid,ModelFolder=cells.Length>9?cells[9]:"",Styles=cells.Length>10?cells[10]:"",StyleReason=cells.Length>11?cells[11]:"",Sample=cells.Length>12?cells[12]:"",StarSource=cells.Length>13?cells[13]:"",Order=profiles.Count};
  }
  if(File.Exists(userNotes))foreach(string line in File.ReadAllLines(userNotes)){
   var c=line.Split(new char[]{'\t'},StringSplitOptions.None);if(c.Length!=2)continue;
   try{notes[c[0]]=Encoding.UTF8.GetString(Convert.FromBase64String(c[1]));}catch(FormatException){}
  }
 }
 public void Add(VoiceProfile p){p.Order=profiles.Count;profiles[p.Key]=p;}
 public int Count {get{return profiles.Count;}}
 public IEnumerable<VoiceProfile> All {get{return profiles.Values.OrderBy(p=>p.Order);}}
 public VoiceProfile Get(string key){VoiceProfile p;if(profiles.TryGetValue(key,out p))return p;return new VoiceProfile{Key=key,Gender="Chưa rõ",Description="Chưa có mô tả riêng. Hãy nghe thử trước khi chọn.",Source="Chưa có dữ liệu"};}
 public string Note(string key){string value;return notes.TryGetValue(key,out value)?value:"";}
 public void SaveNote(string key,string text){
  notes[key]=text??"";var lines=new List<string>();foreach(var pair in notes)lines.Add(pair.Key+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value)));
  string dir=Path.GetDirectoryName(notePath);if(!String.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
  File.WriteAllLines(notePath,lines,new UTF8Encoding(true));
 }
}
}
