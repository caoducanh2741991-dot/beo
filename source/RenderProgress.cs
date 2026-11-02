using System;using System.Linq;using System.Collections.Generic;
namespace V15Voice {
public sealed class RenderProgressSnapshot {
 public int Percent;public double ElapsedSeconds;public double? RemainingSeconds;public DateTime? FinishedUtc;public bool Running;public string Phase="";
 public string Eta {get{if(!Running)return Percent==100?"Đã xong":"—";if(!RemainingSeconds.HasValue)return "Đang ước lượng";return "~"+RenderProgress.FinishTime(RemainingSeconds.Value)+" · còn "+RenderProgress.Duration(RemainingSeconds.Value);}}
}
// Presentation estimates only: these values never decide whether speech fits
// the source timeline. UTC timestamps avoid clock/DST changes in saved data.
public sealed class RenderProgress {
 readonly object gate=new object();readonly Func<DateTime> now;DateTime start,lastAdvance,finished;int percent,advances;double savedElapsed;bool running;string phase="";
 public RenderProgress():this(()=>DateTime.UtcNow){}public RenderProgress(Func<DateTime> clock){now=clock;}
 public void Begin(){lock(gate){start=lastAdvance=now();finished=DateTime.MinValue;percent=advances=0;savedElapsed=0;running=true;phase="Chuẩn bị";}}
 public void Update(int value,string message){lock(gate){if(!running)return;int p=Math.Max(0,Math.Min(99,value));if(p>percent){percent=p;lastAdvance=now();advances++;}phase=message??"";}}
 public void Finish(bool complete){lock(gate){if(running)savedElapsed=Math.Max(0,(now()-start).TotalSeconds);running=false;if(complete){percent=100;finished=now();}else percent=Math.Min(99,percent);}}
 public void Restore(int value,double elapsed,bool complete){lock(gate){running=false;percent=complete?100:Math.Max(0,Math.Min(99,value));savedElapsed=Math.Max(0,elapsed);}}
 public RenderProgressSnapshot Read(){lock(gate){DateTime time=now();double elapsed=running?Math.Max(0,(time-start).TotalSeconds):savedElapsed;double? remaining=null;
   if(running&&elapsed>=5&&percent>=5&&advances>=2&&(time-lastAdvance).TotalSeconds<=60)remaining=Math.Max(1,elapsed*(100.0-percent)/percent);
   return new RenderProgressSnapshot{Percent=percent,ElapsedSeconds=elapsed,Running=running,RemainingSeconds=remaining,Phase=phase,FinishedUtc=finished==DateTime.MinValue?(DateTime?)null:finished};}}
 public static string Duration(double seconds){if(Double.IsNaN(seconds)||Double.IsInfinity(seconds)||seconds<0)return "—";var t=TimeSpan.FromSeconds(Math.Min(seconds,315360000));if(t.TotalDays>=1)return ((int)t.TotalDays)+" ngày "+t.Hours+" giờ";if(t.TotalHours>=1)return ((int)t.TotalHours)+"g "+t.Minutes+"p";return t.Minutes+"p "+t.Seconds+"s";}
 public static string FinishTime(double seconds){var local=DateTime.Now.AddSeconds(Math.Min(315360000,Math.Max(0,seconds)));return local.ToString(local.Date==DateTime.Now.Date?"HH:mm":"dd/MM HH:mm");}
 public static double? QueueRemaining(IEnumerable<BatchJob> input){var jobs=input.ToList();var active=jobs.FirstOrDefault(j=>j.State==JobState.Running);if(active==null)return null;var snapshot=active.Progress.Read();if(!snapshot.RemainingSeconds.HasValue)return null;
  // Other files are scaled by input work size, not treated as measured audio.
  double whole=snapshot.ElapsedSeconds+snapshot.RemainingSeconds.Value,words=Math.Max(1,active.Cues.Sum(c=>c.Words));return snapshot.RemainingSeconds+jobs.Skip(jobs.IndexOf(active)+1).Where(j=>j.State!=JobState.Done&&j.Selected).Sum(j=>whole*Math.Max(1,j.Cues.Sum(c=>c.Words))/words);
 }
}
}
