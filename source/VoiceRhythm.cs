using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Globalization;
namespace V15Voice {
// Small, process-local waveform cache for measured/previewed phrases. No model is retained.
public sealed class SpeechCache {
 sealed class Entry {public string Key,Device;public Wave Audio;}
 readonly List<Entry> items=new List<Entry>();int samples;const int MaxSamples=24000*60;
 static string Key(KokoroEngine e,string text,double speed){if(e.Kind==EngineKind.Rvc)return SpeechArchive.Key(e,text,speed);string stamp="";if(!String.IsNullOrEmpty(e.ReferencePath)&&File.Exists(e.ReferencePath)){var f=new FileInfo(e.ReferencePath);stamp=f.Length+":"+f.LastWriteTimeUtc.Ticks;}return String.Join("\u001f",new[]{e.Root,e.Kind.ToString(),e.ModelFolder,e.Speaker.ToString(),e.VoiceName,e.ReferencePath,e.ReferenceText,stamp,e.DeviceMode,speed.ToString("R",CultureInfo.InvariantCulture),text});}
 public void Clear(){lock(items){items.Clear();samples=0;}}
 public Wave Get(KokoroEngine e,string text,double speed){string k=Key(e,text,speed);lock(items){var x=items.LastOrDefault(v=>v.Key==k);if(x==null)return null;e.ReportActivity("Dùng lại đoạn đã nghe thử / đo nhịp");if(!String.IsNullOrEmpty(x.Device))e.ReportDevice(x.Device);return x.Audio;}}
 public void Put(KokoroEngine e,string text,double speed,Wave w){if(w.Samples.Length>MaxSamples)return;string k=Key(e,text,speed);lock(items){var old=items.FirstOrDefault(x=>x.Key==k);if(old!=null){samples-=old.Audio.Samples.Length;items.Remove(old);}while(items.Count>0&&(items.Count>=32||samples+w.Samples.Length>MaxSamples)){samples-=items[0].Audio.Samples.Length;items.RemoveAt(0);}items.Add(new Entry{Key=k,Device=e.DeviceInfo,Audio=w});samples+=w.Samples.Length;}}
}
public static class VoiceRhythm {
 public static string Measure(List<Cue> cues,KokoroEngine engine,RenderOptions options,string work,Action<int,string> progress){
  if(cues.Count==0)throw new Exception("Mở SRT trước khi đo nhịp.");Directory.CreateDirectory(work);int n=Math.Min(5,cues.Count);var indices=Enumerable.Range(0,n).Select(i=>n==1?0:(int)Math.Round(i*(cues.Count-1.0)/(n-1))).Distinct().ToList();var lines=new List<string>();var factors=new List<double>();
  string prior=engine.RenderContext;try{for(int i=0;i<indices.Count;i++){if(engine.Cancelled)throw new OperationCanceledException();var c=cues[indices[i]];engine.RenderContext="Đo nhịp · khối "+c.Id+"/"+cues.Count;if(progress!=null)progress(i*100/indices.Count,"Đo khối "+c.Id);var p=Punctuation.Generate(engine,c.Text,options.BaseSpeed,options,Path.Combine(work,"measure.wav"));double seconds=p.Join().Duration,available=Math.Max(.04,c.Duration-.025),required=options.BaseSpeed*seconds/available;factors.Add(required);string note=required>Tempo.Maximum(options)?"Câu quá dài":required<Tempo.Minimum(options)?"Câu ngắn, còn khoảng chờ":"Có thể căn nhẹ";lines.Add("#"+c.Id+" · "+Scripts.Stamp(c.Start)+" · đọc "+seconds.ToString("0.00")+"s / khung "+c.Duration.ToString("0.00")+"s · cần khoảng "+required.ToString("0.00")+"× · "+note);}}
  finally{engine.RenderContext=prior;}
  factors.Sort();if(progress!=null)progress(100,"Đã đo nhịp giọng");return "ĐO GIỌNG ĐANG CHỌN BẰNG ÂM THANH THỰC TẾ\r\n"+engine.VoiceName+" · "+indices.Count+" khối đại diện · tốc độ gốc "+options.BaseSpeed.ToString("0.00")+"\r\nGiới hạn căn hiện tại: "+Tempo.Minimum(options).ToString("0.00")+"–"+Tempo.Maximum(options).ToString("0.00")+"×\r\n\r\n"+String.Join("\r\n",lines)+"\r\n\r\nĐây là đo thử trên các khối đã chọn, không bảo đảm mọi câu đều khớp. Khi render, app tiếp tục đo và căn từng khối. Không tự sửa lời hay timecode. Các đoạn đã đo được dùng lại khi cùng giọng, mẫu, lời và tốc độ (bộ nhớ tạm có giới hạn).";
 }
 public static string Status(Cue c,RenderOptions o){string s="Khớp tự nhiên";if(c.Speed>o.BaseSpeed+.001)s=(o.GentleFit?"Đã căn nhẹ":"Đã căn tốc độ")+" · tự tăng tốc "+c.Speed.ToString("0.00");else if(c.Speed<o.BaseSpeed-.001)s=(o.GentleFit?"Đã căn nhẹ":"Đã căn tốc độ")+" · giảm tốc "+c.Speed.ToString("0.00");double spare=c.Duration-c.Actual;if(spare>Math.Max(.5,c.Duration*.20))s="Câu quá ngắn · nghỉ dư "+spare.ToString("0.00")+"s · "+s;return s;}
}
}
