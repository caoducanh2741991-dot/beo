using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace V15Voice {
public static class UserVoices {
 public static List<VoiceProfile> Load(string state){var result=new List<VoiceProfile>();string folder=Path.Combine(state,"user_voices");var xml=StateFiles.Read(Path.Combine(folder,"voices.xml"));if(xml==null)return result;
  foreach(var x in xml.Elements("voice")){string id=StateFiles.S(x,"id");Guid guid;if(!Guid.TryParseExact(id,"N",out guid))continue;string engine=StateFiles.S(x,"engine");if(engine!="Qwen"&&engine!="Chatterbox")continue;
   result.Add(new VoiceProfile{Key="U:"+id,Name=StateFiles.S(x,"name"),Gender=StateFiles.S(x,"gender","Chưa rõ"),Kind=engine=="Qwen"?EngineKind.Qwen:EngineKind.Chatterbox,ModelFolder=engine=="Qwen"?"qwen_base":"chatter_nano",Speaker=-1,ReferencePath=Path.Combine(folder,id+".wav"),ReferenceText=StateFiles.S(x,"transcript"),Styles=StateFiles.S(x,"styles","Kể chuyện"),Description=StateFiles.S(x,"description","Giọng riêng theo mẫu tham chiếu; độ giống cần nghe thử."),Source="Mẫu do người dùng cung cấp • "+engine,StyleReason="Phong cách do người dùng chọn; chưa có đánh giá độc lập.",Grade="Giọng riêng"});
  }return result;
 }
 public static void Save(string state,VoiceProfile profile){string folder=Path.Combine(state,"user_voices");var all=Load(state);all.RemoveAll(v=>v.Key==profile.Key);all.Add(profile);var xml=new XElement("voices");foreach(var p in all)xml.Add(new XElement("voice",new XAttribute("id",p.Key.Substring(2)),new XAttribute("name",p.Name),new XAttribute("gender",p.Gender),new XAttribute("engine",p.Kind.ToString()),new XAttribute("transcript",p.ReferenceText??""),new XAttribute("styles",p.Styles),new XAttribute("description",p.Description)));StateFiles.Write(Path.Combine(folder,"voices.xml"),xml);}
 public static Wave Import(string root,string source,string target,double start,double length,Func<bool> cancelled){
  string ext=Path.GetExtension(source).ToLowerInvariant();if(ext!=".mp3"&&ext!=".wav"&&ext!=".mp4"&&ext!=".m4a")throw new Exception("Chọn mẫu MP3, WAV, MP4 hoặc M4A.");
  if(Double.IsNaN(start)||Double.IsInfinity(start)||start<0||Double.IsNaN(length)||Double.IsInfinity(length)||length<6||length>20)throw new Exception("Chọn đoạn mẫu dài 6–20 giây và vị trí bắt đầu hợp lệ.");
  if(new FileInfo(source).Length>(ext==".mp4"?1024L:200L)*1024*1024)throw new Exception("File quá lớn: MP4 tối đa 1 GB, âm thanh tối đa 200 MB.");
  target=Path.GetFullPath(target);Directory.CreateDirectory(Path.GetDirectoryName(target));string stem=Path.Combine(Path.GetDirectoryName(target),".v15_sample_"+Guid.NewGuid().ToString("N")),raw=stem+".float.wav",pcm=stem+".wav";var ci=System.Globalization.CultureInfo.InvariantCulture;
  try{
   MediaSupport.Ensure(root,cancelled);
   AudioProcess.Run(root,"-v error -y -ss "+start.ToString("0.000",ci)+" -i "+KokoroEngine.Q(source)+" -t "+length.ToString("0.000",ci)+" -map 0:a:0 -vn -ac 1 -ar 24000 -c:a pcm_f32le "+KokoroEngine.Q(raw),cancelled);
   var stats=FinalAudio.Measure(root,raw,cancelled);double gain=Double.IsInfinity(stats.TruePeak)?0:Math.Min(0,-1-stats.TruePeak);
   AudioProcess.Run(root,"-v error -y -i "+KokoroEngine.Q(raw)+" -af volume="+gain.ToString("0.000000",ci)+"dB -ac 1 -ar 24000 -c:a pcm_s16le "+KokoroEngine.Q(pcm),cancelled);
   var wave=Wave.Read(pcm);if(wave.Duration<length-.1)throw new Exception("Không đủ "+length.ToString("0.0",ci)+" giây ở vị trí đã chọn. Hãy giảm thời lượng hoặc chọn đoạn sớm hơn.");
   double rms=Math.Sqrt(wave.Samples.Average(x=>(double)x*x));if(rms<50)throw new Exception("Mẫu quá nhỏ hoặc gần như im lặng. Chọn đoạn có lời đọc rõ.");
   if(cancelled())throw new OperationCanceledException();if(File.Exists(target))File.Replace(pcm,target,null);else File.Move(pcm,target);return wave;
  }catch(Exception e){throw MediaSupport.InputError(source,e);}finally{try{File.Delete(raw);}catch{}try{File.Delete(pcm);}catch{}}
 }
}
}
