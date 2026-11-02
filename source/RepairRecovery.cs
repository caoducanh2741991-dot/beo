using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace V15Voice {
public static class RepairRecovery {
 public static string RowDetail(AiRepairRow r){var m=r.CurrentMeasure;return r.Status+" · Câu "+r.Stage.Cues.First().Id+"–"+r.Stage.Cues.Last().Id+(m==null?" · chưa đo":m.Error.Length>0?" · "+m.Kind:" · "+(m.Delta<0?"Thiếu ":"Dư ")+Math.Abs(m.Delta).ToString("0.000")+"s");}
 public static string Problem(AiRepairSession p){return String.Join("; ",p.Rows.Where(r=>r.CurrentMeasure==null||!r.CurrentMeasure.Valid||r.CurrentMeasure.NeedsRepair).Select(r=>"cue "+r.Stage.Cues.First().Id+"–"+r.Stage.Cues.Last().Id+" | "+(r.Error.Length>0?r.Error:r.CurrentMeasure==null?"chưa đo":r.CurrentMeasure.Error.Length>0?r.CurrentMeasure.Kind+": "+r.CurrentMeasure.Error:(r.CurrentMeasure.Delta<0?"Thiếu ":"Dư ")+Math.Abs(r.CurrentMeasure.Delta).ToString("0.000")+"s")+" | SỬA NGAY"));}
 public static void Hold(BatchJob job,AiRepairSession p){p.Save();job.RepairSession=p;job.Cues=p.Effective();job.AiReport=p.Report;job.AiSnapshot=AiRepairSession.SnapshotHash(job.Cues);job.CheckpointCues=null;job.State=p.Rows.Any(r=>r.BaseStatus=="ENGINE_ERROR"||r.BaseStatus=="RENDER_FAILED")?JobState.Failed:JobState.NeedsReview;job.FailureCode=job.State==JobState.Failed?(p.Rows.Any(r=>r.BaseStatus=="ENGINE_ERROR")?"ENGINE_ERROR":"RENDER_FAILED"):"NEEDS_REVIEW";job.Error=job.FailureCode+" · "+Problem(p);job.Warning="Checkpoint đã giữ; sửa tại dòng rồi tiếp tục.";foreach(var row in p.Rows)foreach(var c in job.Cues.Where(c=>row.Stage.Cues.Any(x=>x.Id==c.Id)))c.Status=RowDetail(row);}
 public static void Record(RenderOptions o,List<Cue> input,KokoroEngine engine,string requested){var p=o.AiRepair;if(p==null)return;var report=new VoiceRenderReport(input,engine,o,requested);o.RunReport=report;report.Pending(p);}
}
public sealed partial class VoiceRenderReport {
 public void Pending(AiRepairSession p){string code=p.Rows.Any(r=>r.BaseStatus=="ENGINE_ERROR")?"ENGINE_ERROR":p.Rows.Any(r=>r.BaseStatus=="RENDER_FAILED")?"RENDER_FAILED":"NEEDS_REVIEW";SnapshotRepair(p,code,null);}
 public void SnapshotRepair(AiRepairSession p,string code,Exception error){
  PlanStages(p.Rows.Select(r=>r.Stage));foreach(var r in p.Rows){
   var stage=Stages.First(s=>s.Number==r.Stage.Number);stage.Text=String.Join(" ",r.CurrentWords.Select(c=>c.Text));var m=r.CurrentMeasure;
   if(m==null){if(code=="CANCELLED"&&p.ActiveStage==r.Stage.Number){stage.Status="Đã dừng tại chặng";stage.Reason="Dừng trong bước "+p.ActivePhase+"; chưa có số đo hoàn chỉnh.";}continue;}
   stage.Before=m.Natural>0?(double?)m.Natural:null;stage.After=m.Error.Length==0&&m.Valid?(double?)m.After:null;stage.Speed=m.Error.Length==0&&m.Valid?(double?)m.Speed:null;stage.Tts=m.Tts>0?(double?)m.Tts:null;
   stage.AppPauses=m.Natural>0?(double?)m.AppPauses:null;stage.Breaths=m.Natural>0?(double?)m.Breaths:null;
   stage.Status=r.BaseStatus=="ENGINE_ERROR"||r.BaseStatus=="RENDER_FAILED"?r.BaseStatus:m.Error.Length>0?(m.Kind=="AUDIO ERROR"?"RENDER_FAILED":"ENGINE_ERROR"):m.NeedsRepair||!m.Valid?"NEEDS_REVIEW":"Đạt khung chặng";
   stage.Reason=m.Detail+(r.Error.Length>0?" · "+r.Error:"");if(!String.IsNullOrEmpty(m.DeviceInfo)&&!devices.Contains(m.DeviceInfo))devices.Add(m.DeviceInfo);
  }
  var first=p.Rows.FirstOrDefault(r=>r.CurrentMeasure==null||!r.CurrentMeasure.Valid||r.CurrentMeasure.NeedsRepair);
  Current=null;CurrentStage=null;StepCode="REPAIR_RECOVERY";StepName="Khôi phục số đo thực từ checkpoint · "+p.ActivePhase;Finish(code,"",error);
  FailureCode=code;FailureStage=code=="CANCELLED"&&p.ActiveStage>0?p.ActiveStage:first==null?(int?)null:first.Stage.Number;FailureStep=StepCode;FailureStepName=StepName;
  FailureDetail=(error==null?"":error.ToString()+"\r\n")+RepairRecovery.Problem(p).Replace(" | SỬA NGAY","");
 }
}
}
