using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Globalization;using System.Xml.Linq;using System.Collections.Concurrent;using System.Threading.Tasks;
namespace V15Voice {
// Own one measuring engine at a time. Release it before language-model inference
// and speaker switches so an 8 GB device never acquires two resident models.
public sealed class RepairVoiceLease:IDisposable {
 readonly Func<string,KokoroEngine> factory;KokoroEngine current;string role;
 public RepairVoiceLease(Func<string,KokoroEngine> create){factory=create;}
 public KokoroEngine Get(string speaker){if(current!=null&&role!=speaker)Release();if(current==null){current=factory(speaker);role=speaker;if(RenderPerformance.Current!=null)RenderPerformance.Current.Count("AI_VOICE_ENGINE_CREATED");}return current;}
 public void Release(){var old=current;current=null;role=null;if(old!=null)old.Dispose();}
 public void Dispose(){Release();}
}
public sealed class LockedCueAudio {
 public Wave Natural,Audio;public double Tts,Pauses,Speed,Reserve;public string Origin="";
}
// Only bounded PCM I/O overlaps the following synthesis/fit. No second model is
// loaded. Dispose drains successful cue writes before the render call returns.
public sealed class CueCheckpointWriter:IDisposable {
 readonly BlockingCollection<Action> pending=new BlockingCollection<Action>(2);readonly Task worker;bool disposed;
 public CueCheckpointWriter(Action<string> activity){worker=Task.Run(()=>{foreach(var write in pending.GetConsumingEnumerable())try{write();}catch(Exception ex){try{if(activity!=null)activity("Không lưu được PCM cue; WAV vẫn được kiểm tra bình thường: "+ex.Message);}catch{}}});}
 public void Add(Action write){pending.Add(write);}
 public void Dispose(){if(disposed)return;disposed=true;pending.CompleteAdding();try{worker.GetAwaiter().GetResult();}finally{pending.Dispose();}}
}
public static class LockedCueReuse {
 static bool Allowed(KokoroEngine e,RenderOptions o){return o.Locked&&!o.Prose&&!o.Anchored&&(e.Archive==null||!e.Archive.Refresh);}
 static string Folder(Cue cue,Cue next,KokoroEngine e,RenderOptions o,string profile){string home=String.IsNullOrEmpty(o.CheckpointRoot)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"V15VoiceStudio","stage-checkpoints"):o.CheckpointRoot;string source=o.ReportContext==null?"":o.ReportContext.SourcePath;return Path.Combine(home,"locked-cues",SpeechArchive.Hash("locked411-v1|"+profile+"|"+source+"|"+AiRepairSession.SnapshotHash(new[]{cue})+"|"+(next==null?"LAST":next.Start.ToString("R",CultureInfo.InvariantCulture))));}
 static LockedCueAudio Load(AiMeasurement m,Cue cue,double reserve,RenderOptions o,string origin){
  if(m==null||!m.Fits||m.NeedsRepair||new[]{m.Target,m.Speed,m.Natural,m.After,m.Tts,m.AppPauses}.Any(x=>Double.IsNaN(x)||Double.IsInfinity(x))||!m.Valid)return null;
  double usedReserve=cue.Duration-m.Target,min=o.SlowShort?Tempo.Minimum(o):o.BaseSpeed;
  if(Double.IsNaN(usedReserve)||usedReserve<-.00001||usedReserve>reserve+.00001||m.Speed<min-.00001||m.Speed>Tempo.Maximum(o)+.00001)return null;
  if(m.Tts<=0||m.AppPauses<0||m.Tts+m.AppPauses>m.Natural+.002)return null;
  var a=Wave.Read(m.Audio);var n=Wave.Read(m.NaturalAudio);
  if(a.Rate!=24000||n.Rate!=24000||Math.Abs(a.Duration-m.After)>.00005||Math.Abs(n.Duration-m.Natural)>.00005||a.Duration>m.Target+.00001)return null;
  return new LockedCueAudio{Natural=n,Audio=a,Tts=m.Tts,Pauses=m.AppPauses,Speed=m.Speed,Reserve=Math.Max(0,usedReserve),Origin=origin};
 }
 public static LockedCueAudio Read(Cue cue,Cue next,KokoroEngine e,RenderOptions o,double reserve){
  if(!Allowed(e,o)||(o.AiRepair==null&&!o.ReuseLockedAudio))return null;string profile=RenderCheckpoint.Profile(e,o);
  try{if(o.AiRepair!=null&&!o.AiRepair.Anchored){var following=o.AiRepair.Rows.SelectMany(r=>r.CurrentWords).FirstOrDefault(c=>c.Id>cue.Id);bool sameNext=following==null?next==null:next!=null&&following.Start==next.Start;var row=o.AiRepair.Rows.FirstOrDefault(r=>r.Stage.Cues.Count==1&&r.Stage.Cues[0].Id==cue.Id);if(sameNext&&row!=null&&AiRepairSession.SnapshotHash(row.CurrentWords)==AiRepairSession.SnapshotHash(new[]{cue})){var m=row.CurrentMeasure;if(m!=null&&m.ProfileSignature==profile){var ready=Load(m,cue,reserve,o,"AI_MEASURED_PCM");if(ready!=null)return ready;}}}}catch{}
  if(!o.ReuseLockedAudio)return null;
  try{var m=AiRepairSession.ReadMeasurement(StateFiles.Read(Path.Combine(Folder(cue,next,e,o,profile),"cue.xml")));return m!=null&&m.ProfileSignature==profile?Load(m,cue,reserve,o,"LOCKED_CHECKPOINT"):null;}catch{return null;}
 }
 public static Action PrepareWrite(Cue cue,Cue next,KokoroEngine e,RenderOptions o,Wave natural,Wave audio,double tts,double pauses,double reserve){
  if(!o.ReuseLockedAudio||!Allowed(e,o)||audio.Duration>cue.Duration-reserve+.00001)return null;
  // Snapshot all mutable voice/option/cue data on the render thread.
  string profile=RenderCheckpoint.Profile(e,o),folder=Folder(cue,next,e,o,profile),stem=Guid.NewGuid().ToString("N"),a=Path.Combine(folder,stem+".wav"),n=Path.Combine(folder,stem+"-natural.wav");
  var m=new AiMeasurement{Fits=true,Target=cue.Duration-reserve,Natural=natural.Duration,After=audio.Duration,Speed=cue.Speed,Tts=tts,AppPauses=pauses,Audio=a,NaturalAudio=n,ProfileSignature=profile};var perf=o.Performance;
  return ()=>{using(perf==null?null:perf.Time("CUE_CHECKPOINT_IO")){try{Directory.CreateDirectory(folder);natural.Write(n);audio.Write(a);m.Hash=StateFiles.Hash(a);m.NaturalHash=StateFiles.Hash(n);StateFiles.Write(Path.Combine(folder,"cue.xml"),AiRepairSession.MeasurementXml("cue",m));}catch{try{File.Delete(a);File.Delete(n);}catch{}throw;}}};
 }
}
public sealed partial class VoiceRenderReport {
 public void CachedLockedCue(LockedCueAudio c,Cue cue){Origin(true);lock(sync){if(Current==null)return;Current.CachedFit=true;Current.Before=c.Natural.Duration;Current.WorkingBefore=options.Punctuation==PunctuationMode.Auto?(double?)c.Natural.Duration:null;Current.Tts=c.Tts;Current.PauseBefore=c.Pauses;Current.Reserve=c.Reserve;Current.Target=cue.Duration-c.Reserve;Current.Detail="Dùng PCM đã đo/căn, kiểm tra hash và cấu hình; không chạy lại TTS/atempo. "+c.Origin;Append(Current.Detail);}}
}
}
