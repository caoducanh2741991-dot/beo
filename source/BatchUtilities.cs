using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace V15Voice {
// Workers publish only their latest UI state. The UI drains it on its own timer.
// Audio, checkpoints and render reports still receive every original event.
public sealed class LatestRenderUpdate {
 readonly object gate=new object();bool changed;int percent;string message="",device="";
 public long Published,Drained;
 public void Publish(int value,string text){lock(gate){percent=value;message=text??"";changed=true;Published++;}}
 public void Device(string text){lock(gate){device=text??"";changed=true;Published++;}}
 public bool Take(out int value,out string text,out string hardware){lock(gate){value=percent;text=message;hardware=device;if(!changed)return false;changed=false;Drained++;return true;}}
 public void Clear(){lock(gate){changed=false;message=device="";percent=0;}}
}
public sealed class BatchImportResult {
 public List<BatchJob> Jobs=new List<BatchJob>();public List<string> Warnings=new List<string>();public int Duplicates,Generated,Unsupported,OverLimit;
 public string Summary {get{return "Thêm "+Jobs.Count+" file · bỏ "+Duplicates+" trùng · "+Generated+" SRT đầu ra · "+OverLimit+" vượt giới hạn"+(Warnings.Count>0?" · "+Warnings.Count+" mục chưa đọc được":"");}}
}
public static class BatchImport {
 public static bool Generated(string path){string n=Path.GetFileName(path);return n.EndsWith(".final.srt",StringComparison.OrdinalIgnoreCase)||n.EndsWith(".aligned.srt",StringComparison.OrdinalIgnoreCase);}
 public static BatchImportResult Read(IEnumerable<string> inputs,IEnumerable<string> existing,int capacity,Func<bool> cancel,Action<string> activity=null){
  var r=new BatchImportResult();var seen=new HashSet<string>(existing.Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase);var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var paths=new List<string>();
  Action<string,bool> add=(path,fromFolder)=>{if(cancel())throw new OperationCanceledException();if(fromFolder&&Generated(path)){r.Generated++;return;}string ext=Path.GetExtension(path);if(fromFolder&&!ext.Equals(".srt",StringComparison.OrdinalIgnoreCase)){r.Unsupported++;return;}if(!fromFolder&&!ext.Equals(".srt",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".txt",StringComparison.OrdinalIgnoreCase)){r.Unsupported++;return;}string full=Path.GetFullPath(path);if(!seen.Add(full)){r.Duplicates++;return;}if(paths.Count>=Math.Max(0,capacity)){r.OverLimit++;return;}paths.Add(full);};
  var pending=new Queue<string>();foreach(string item in inputs){if(cancel())throw new OperationCanceledException();try{if(Directory.Exists(item))pending.Enqueue(Path.GetFullPath(item));else add(item,false);}catch(OperationCanceledException){throw;}catch(Exception ex){r.Warnings.Add(item+": "+ex.Message);}}
  while(pending.Count>0){if(cancel())throw new OperationCanceledException();string dir=pending.Dequeue();if(!folders.Add(dir))continue;try{if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0){r.Warnings.Add(dir+": bỏ qua thư mục liên kết");continue;}if(activity!=null)activity("Đọc thư mục SRT · "+dir);foreach(string f in Directory.GetFiles(dir).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))add(f,true);foreach(string sub in Directory.GetDirectories(dir).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)){string name=Path.GetFileName(sub);if(name.StartsWith(".v15",StringComparison.OrdinalIgnoreCase)||name.Equals("BEO_CREATOR",StringComparison.OrdinalIgnoreCase))continue;pending.Enqueue(sub);}}catch(OperationCanceledException){throw;}catch(Exception ex){r.Warnings.Add(dir+": "+ex.Message);}}
  foreach(string p in paths){if(cancel())throw new OperationCanceledException();r.Jobs.Add(BatchJob.Load(p));}return r;
 }
}
public static class BatchValidity {
 public static string Key(string batchKey,BatchJob job){return SpeechArchive.Hash(batchKey+"|"+job.TargetSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
 public static void Refresh(BatchJob job,string currentKey){
  bool changed=false;try{changed=!File.Exists(job.InputPath)||StateFiles.Hash(job.InputPath)!=job.SourceHash;}catch{changed=true;}
  if(changed||job.State==JobState.ImportFailed){var loaded=BatchJob.Load(job.InputPath);job.Cues=loaded.Cues;job.SourceHash=loaded.SourceHash;job.Timed=loaded.Timed;job.State=loaded.State;job.Error=loaded.Error;job.FailureCode=loaded.FailureCode;job.AiReport=job.AiSnapshot="";job.RepairSession=null;job.RunSettingsKey="";job.Progress.Restore(0,0,false);if(changed)job.Warning="File nguồn đã thay đổi · kiểm tra lại khi chạy";}
  if(job.State==JobState.Done){string srt=String.IsNullOrEmpty(job.OutputPath)?"":Path.Combine(Path.GetDirectoryName(job.OutputPath),Path.GetFileNameWithoutExtension(job.OutputPath)+".final.srt");if(job.RunSettingsKey!=currentKey||!File.Exists(job.OutputPath)||!File.Exists(srt)||!File.Exists(job.ReportPath)||(job.WorkflowMode==2&&!File.Exists(job.ShortReportPath))){job.State=JobState.Queued;job.Progress.Restore(0,0,false);job.Warning="Nguồn/cấu hình hoặc bộ đầu ra thay đổi · dùng cache còn hợp lệ khi chạy lại";}}
 }
}
// A burst of short-lived voice engines used to sort the same cache directory
// on every construction. Keep maintenance bounded without changing cache keys.
public static class ArchiveMaintenance {
 sealed class Window {public DateTime At;public long Added;}
 static readonly object gate=new object();static readonly Dictionary<string,Window> windows=new Dictionary<string,Window>(StringComparer.OrdinalIgnoreCase);
 public static bool Due(string folder,long written=0){lock(gate){Window w;if(!windows.TryGetValue(folder,out w)){windows[folder]=new Window{At=DateTime.UtcNow};return true;}w.Added+=written;if((DateTime.UtcNow-w.At).TotalSeconds<30&&w.Added<32L*1024*1024)return false;w.At=DateTime.UtcNow;w.Added=0;return true;}}
 public static void Failed(string folder){lock(gate)windows.Remove(folder);}
}
}
