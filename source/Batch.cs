using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace V15Voice {
public enum JobState { Queued, ImportFailed, Running, Done, Failed, Cancelled }
public sealed class BatchJob {
 public string AiReport="",AiSnapshot="";public int RepairMode=-1;public double TargetSeconds;public string ReportPath="";public string TimelineReport="",Warning="";public double AudioDuration;public bool DurationFinal;public string SourceHash="";
 public string InputPath,Error="",OutputPath="";public bool Timed;
 public List<Cue> CheckpointCues;public List<Cue> Cues=new List<Cue>();public JobState State=JobState.Queued;
 public string Name {get{return Path.GetFileName(InputPath);}}
 public string Status {get{switch(State){case JobState.ImportFailed:return "Lỗi nhập file";case JobState.Running:return "Đang chạy";case JobState.Done:return String.IsNullOrEmpty(Warning)?"Hoàn tất":"WAV xong · cần duyệt";case JobState.Failed:return "Lỗi tạo voice";case JobState.Cancelled:return "Đã dừng";default:return "Đang chờ";}}}
 public static BatchJob Load(string path){
  var j=new BatchJob{InputPath=Path.GetFullPath(path)};
  try {
   string ext=Path.GetExtension(path).ToLowerInvariant();if(ext==".mp3"||ext==".wav")throw new Exception("Mẫu âm thanh cần nhập ở tab Sao chép giọng MP3 / WAV. Tại đây chỉ nhận SRT / TXT.");if(ext!=".srt"&&ext!=".txt")throw new Exception("Chỉ nhận SRT hoặc TXT.");
   if(new FileInfo(path).Length>5*1024*1024)throw new Exception("Mỗi kịch bản tối đa 5 MB.");
   j.SourceHash=StateFiles.Hash(path);string text=File.ReadAllText(path);j.Timed=ext==".srt"||text.Contains("-->")||Regex.IsMatch(text,@"\d+:\d{2}:\d{2}");
   j.Cues=ext==".srt"?Scripts.ParseSrt(text):j.Timed?Scripts.ParseText(text):LongText.Parse(text);
  }catch(Exception e){j.State=JobState.ImportFailed;j.Error=e.Message;}
  return j;
 }
}
public static class BatchRunner {
 public static string Run(List<BatchJob> jobs,bool skipDone,Func<bool> cancelled,Func<BatchJob,string,Action<int,string>,string> render,Action<int,string> progress){
  if(jobs.Count==0)throw new Exception("Hàng đợi chưa có file.");
  int total=jobs.Count(j=>!(skipDone&&j.State==JobState.Done)),finished=0;string last="";
  foreach(var j in jobs){
   if(skipDone&&j.State==JobState.Done){if(File.Exists(j.OutputPath))last=j.OutputPath;continue;}
   if(cancelled())break;
   if(j.State==JobState.ImportFailed){finished++;progress(total==0?0:finished*100/total,j.Name+": lỗi nhập file");continue;}
   j.CheckpointCues=j.Cues.Select(c=>new Cue{Id=c.Id,Start=c.Start,End=c.End,Text=c.Text,Speaker=c.Speaker,Prose=c.Prose,ParagraphEnd=c.ParagraphEnd,AnchorAfter=c.AnchorAfter,VoiceLock=c.VoiceLock}).ToList();j.State=JobState.Running;j.Warning=j.TimelineReport=j.Error="";j.OutputPath="";progress(total==0?0:finished*100/total,j.Name+": bắt đầu");
   try {
    j.OutputPath=render(j,OutputFiles.Beside(j.InputPath),(p,message)=>progress(total==0?0:(finished*100+Math.Min(100,Math.Max(0,p)))/total,j.Name+" • "+message));
    j.State=JobState.Done;j.CheckpointCues=null;last=j.OutputPath;
   }catch(OperationCanceledException){j.Cues=j.CheckpointCues;j.CheckpointCues=null;j.State=JobState.Cancelled;j.Error="Đã dừng theo yêu cầu.";break;}
   catch(Exception e){j.Cues=j.CheckpointCues;j.CheckpointCues=null;j.State=JobState.Failed;j.Error=e.Message;}
   finished++;progress(total==0?100:finished*100/total,j.Name+": "+j.Status);
  }
  return last;
 }
}
}
