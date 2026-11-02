using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace V15Voice {
public sealed class WorkflowResult {
 public string SourceHash="";public List<Cue> Cues=new List<Cue>();public WorkflowBundle Bundle;public Exception Error;public string Status="RENDER_FAILED";
 public bool Rendered{get{return Bundle!=null&&!String.IsNullOrEmpty(Bundle.Wave);}}
}
public static class WorkflowRunner {
 public static List<Cue> SourceWords(string source,List<Cue> draft){string hash;return SourceWords(source,draft,out hash);}
 static List<Cue> SourceWords(string source,List<Cue> draft,out string hash){
  hash="";if(String.IsNullOrEmpty(source))return VoiceRenderReport.Snapshot(draft);
  var load=BatchJob.Load(source);if(load.State==JobState.ImportFailed)throw new IOException("INPUT_ERROR: "+load.Error);if(StateFiles.Hash(source)!=load.SourceHash)throw new IOException("INPUT_ERROR: file nguồn thay đổi trong lúc đọc.");hash=load.SourceHash;
  foreach(var c in load.Cues){var old=draft.FirstOrDefault(x=>x.Id==c.Id&&x.Start==c.Start&&x.End==c.End);if(old!=null){c.AnchorAfter=old.AnchorAfter;if(String.IsNullOrEmpty(c.Speaker))c.Speaker=old.Speaker;}}
  return load.Cues;
 }
 public static WorkflowResult Run(List<Cue> input,KokoroEngine engine,RenderOptions o,string output,int mode,
  Func<List<Cue>,RenderOptions,string,KokoroEngine,Action<int,string>,List<Cue>> prepare,
  Action<int,string> progress,Exception inputFailure=null){
  if(mode!=1&&mode!=2)throw new ArgumentOutOfRangeException("mode");
  var result=new WorkflowResult();o.WorkflowMode=mode;o.WorkflowBundle=null;o.WorkflowOriginal=VoiceRenderReport.Snapshot(input);o.AiRepair=null;o.AiReportPath="";o.RepairStages=null;o.JoinSentences=false;o.Translation=null;o.RunReport=null;o.AnchorResult=null;o.TargetResult=null;
  string source=o.ReportContext==null?"":o.ReportContext.SourcePath;var words=VoiceRenderReport.Snapshot(input);
  try{
   if(inputFailure!=null)throw inputFailure;
   string hash;words=SourceWords(source,input,out hash);o.WorkflowSourceHash=result.SourceHash=hash;o.WorkflowOriginal=VoiceRenderReport.Snapshot(words);if(words.Any(c=>c.Prose)){o.Prose=true;o.Locked=o.Anchored=false;}if(o.ReportContext!=null)o.ReportContext.SourceCues=VoiceRenderReport.Snapshot(words);
   if(engine.Cancelled)throw new OperationCanceledException();
   if(mode==2&&prepare!=null&&!o.Prose&&(o.Locked||o.Anchored)){
    words=prepare(words,o,source,engine,progress);if(o.AiRepair!=null&&!o.AiRepair.Ready){
     RepairRecovery.Record(o,words,engine,output);string code=o.AiRepair.Rows.Any(r=>r.BaseStatus=="ENGINE_ERROR")?"ENGINE_ERROR":o.AiRepair.Rows.Any(r=>r.BaseStatus=="RENDER_FAILED")?"RENDER_FAILED":"NEEDS_REVIEW";
     engine.ReleaseModels();o.Translation=WorkflowReports.Translate(o,words,()=>engine.Cancelled,progress);
     result.Bundle=WorkflowReports.Publish(null,output,words,o,o.RunReport,code);result.Cues=VoiceRenderReport.Snapshot(words);result.Status=code;return result;
    }
   }
   if(o.ReportContext!=null)o.ReportContext.SourceCues=VoiceRenderReport.Snapshot(words);Renderer.Render(words,engine,o,output,progress);result.Bundle=o.WorkflowBundle;result.Status=o.WorkflowBundle.Status;result.Cues=VoiceRenderReport.Snapshot(words);return result;
  }catch(Exception e){
   result.Error=e;if(o.AiRepair!=null)words=o.AiRepair.Effective();result.Cues=VoiceRenderReport.Snapshot(words);
   string code=e is OperationCanceledException?"CANCELLED":e.Message.StartsWith("INPUT_ERROR")||inputFailure!=null?"INPUT_ERROR":o.RunReport!=null&&!String.IsNullOrEmpty(o.RunReport.FailureCode)?Normalize(o.RunReport.FailureCode):e.Data.Contains("V15Status")?Convert.ToString(e.Data["V15Status"]):"ENGINE_ERROR";
   if(o.RunReport==null){o.RunReport=new VoiceRenderReport(words,engine,o,output);o.RunReport.Step("INPUT","Lỗi đầu vào hoặc chuẩn bị");o.RunReport.Finish(e is OperationCanceledException?"ĐÃ DỪNG":"THẤT BẠI","",e);}
   // A failed/cancelled task must still finish with the selected report count.
   // Never invoke a language model after the user has cancelled.
   if(mode==2&&!(e is OperationCanceledException)&&WorkflowReports.Changed(o,words).Count>0&&o.Translation==null){try{engine.ReleaseModels();o.Translation=WorkflowReports.Translate(o,words,()=>engine.Cancelled,progress);}catch(OperationCanceledException){code="CANCELLED";}}
   result.Bundle=WorkflowReports.Publish(null,output,words,o,o.RunReport,code,e);result.Status=code;return result;
  }
 }
 static string Normalize(string code){if(code=="CANCELLED"||code=="NEEDS_REVIEW"||code=="TRANSLATION_INCOMPLETE")return code;if(code.Contains("TIMELINE")||code=="AI_REWRITE_FAILED")return "NEEDS_REVIEW";if(code.Contains("TTS")||code.Contains("MODEL")||code=="ENGINE_ERROR")return "ENGINE_ERROR";if(code=="INPUT_OR_SETUP_FAILED")return "INPUT_ERROR";return "RENDER_FAILED";}
}
}
