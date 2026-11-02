using System;using System.IO;using System.Drawing;using System.Reflection;using System.Windows.Forms;using System.ComponentModel;
namespace V15Voice {
public interface IReviewPlayer {bool Ready{get;}double Position{get;set;}double Duration{get;}bool Muted{set;}int Volume{set;}void Load(string file);void Play();void Pause();}
public sealed class ReviewTransport {
 readonly IReviewPlayer video,audio;public double Start,End,AudioLength;public bool NewVoice=true,Loop=true,Playing;bool started;double? resume;DateTime requested;
 public ReviewTransport(IReviewPlayer v,IReviewPlayer a){video=v;audio=a;}
 public void Load(string movie,string wav,double start,double end,double length){Pause();resume=null;Start=start;End=end;AudioLength=length;video.Load(movie);if(File.Exists(wav))audio.Load(wav);else if(NewVoice)throw new Exception("Chặng này chưa có audio đã đo. Chạy tạo voice trước hoặc chọn âm gốc.");}
 public void Play(){requested=DateTime.UtcNow;Playing=true;started=false;}
 public void Pause(){if(started)try{resume=video.Position;}catch{}Playing=false;started=false;video.Pause();audio.Pause();}
 public void Seek(double seconds){video.Position=Math.Max(0,seconds);if(NewVoice&&audio.Ready)audio.Position=Math.Max(0,seconds-Start);}
 public void Tick(){if(!Playing)return;if(!video.Ready||(NewVoice&&!audio.Ready)){if((DateTime.UtcNow-requested).TotalSeconds>15){Pause();throw new Exception("Bộ phát không mở được video/WAV. Kiểm tra định dạng và thành phần Windows Media Player Legacy / Media Feature Pack.");}return;}
  if(!started){video.Muted=NewVoice;audio.Muted=!NewVoice;if(video.Duration>0&&Start>=video.Duration){Pause();throw new Exception("Mốc chặng nằm ngoài thời lượng video; kiểm tra cặp video/SRT.");}Seek(resume??Start);resume=null;video.Play();if(NewVoice)audio.Play();started=true;}
  double t=video.Position,limit=Start+Math.Max(End-Start,NewVoice?AudioLength:0);if(video.Duration>0)limit=Math.Min(limit,video.Duration);
  if(t>=limit-.015){if(Loop){Seek(Start);video.Play();if(NewVoice)audio.Play();}else Pause();return;}
  if(NewVoice){double expected=t-Start;if(expected<0||expected>=AudioLength){audio.Pause();}else if(Math.Abs(audio.Position-expected)>.12){audio.Position=expected;audio.Play();}}
 }
 public double Position{get{try{return video.Position;}catch{return Start;}}}
 public void SetVolume(int value){video.Volume=value;audio.Volume=value;}
}
// Late-bound Windows Media Player ActiveX avoids shipping generated interop assemblies.
public sealed class ReviewMediaHost:AxHost,IReviewPlayer {
 public ReviewMediaHost():base("6BF52A52-394A-11d3-B153-00C04F79FAA6"){}
 object Get(object obj,string key){return obj.GetType().InvokeMember(key,BindingFlags.GetProperty,null,obj,null);}
 void Set(object obj,string key,object value){obj.GetType().InvokeMember(key,BindingFlags.SetProperty,null,obj,new[]{value});}
 void Call(string method){var c=Get(GetOcx(),"controls");c.GetType().InvokeMember(method,BindingFlags.InvokeMethod,null,c,null);}
 public bool Ready{get{try{return Convert.ToInt32(Get(GetOcx(),"openState"))==13;}catch{return false;}}}
 public double Position{get{return Convert.ToDouble(Get(Get(GetOcx(),"controls"),"currentPosition"));}set{Set(Get(GetOcx(),"controls"),"currentPosition",value);}}
 public double Duration{get{try{return Convert.ToDouble(Get(Get(GetOcx(),"currentMedia"),"duration"));}catch{return 0;}}}
 public bool Muted{set{Set(Get(GetOcx(),"settings"),"mute",value);}}
 public int Volume{set{Set(Get(GetOcx(),"settings"),"volume",Math.Max(0,Math.Min(100,value)));}}
 public void Load(string file){if(!File.Exists(file))throw new FileNotFoundException("Không thấy media.",file);Set(GetOcx(),"uiMode","none");Set(Get(GetOcx(),"settings"),"autoStart",false);Set(GetOcx(),"URL",Path.GetFullPath(file));}
 public void Play(){Call("play");}public void Pause(){try{Call("pause");}catch{}}
}
}
