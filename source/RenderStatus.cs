using System;using System.Linq;using System.Collections.Generic;
namespace V15Voice {
public static class RenderStatus {
 public static string Normalize(string code){
  code=code??"";if(new[]{"CANCELLED","NEEDS_REVIEW","TRANSLATION_INCOMPLETE","FINAL_VERIFIED","ENGINE_ERROR","RENDER_FAILED","INPUT_ERROR"}.Contains(code))return code;
  if(code.Contains("TIMELINE")||code=="AI_REWRITE_FAILED"||code=="SEMANTIC_REVIEW_REQUIRED")return "NEEDS_REVIEW";
  if(code.Contains("TTS")||code.Contains("MODEL"))return "ENGINE_ERROR";
  if(code=="INPUT_OR_SETUP_FAILED"||code.StartsWith("INPUT_"))return "INPUT_ERROR";return "RENDER_FAILED";
 }
 public static string Stage(string status){
  if(status=="Đạt khung chặng"||status=="PASS"||status=="AI_FIXED"||status=="MANUAL_FIXED")return "PASS";
  if(status=="Không khớp khung chặng"||status.StartsWith("NEEDS_REVIEW"))return "NEEDS_REVIEW";
  if(status=="Đã dừng tại chặng"||status=="CANCELLED")return "CANCELLED";
  if(status=="Chưa chạy tới"||status=="UNMEASURED")return "UNMEASURED";
  if(status=="Đang đọc/căn")return "IN_PROGRESS";
  if(status=="ENGINE_ERROR")return "ENGINE_ERROR";return "RENDER_FAILED";
 }
 public static string Summary(IEnumerable<VoiceReportStage> rows){var counts=rows.GroupBy(s=>Stage(s.Status)).ToDictionary(g=>g.Key,g=>g.Count());return "Trạng thái chặng: "+String.Join("; ",new[]{"PASS","NEEDS_REVIEW","ENGINE_ERROR","RENDER_FAILED","CANCELLED","UNMEASURED","IN_PROGRESS"}.Select(s=>s+"="+(counts.ContainsKey(s)?counts[s]:0)))+"; TOTAL="+rows.Count()+". Đạt chặng chưa có nghĩa đã xuất WAV cuối.";}
}
public sealed partial class VoiceRenderReport {
 public string WaveStatus{get{return Wav.Length>0?"FINAL_VERIFIED":!Complete?"IN_PROGRESS":RenderStatus.Normalize(FailureCode.Length>0?FailureCode:Outcome);}}
 public string TranslationStatus{get{return options.WorkflowMode>0?(options.WorkflowMode==1||WorkflowReports.Changed(options,sources).Count==0?"NOT_REQUIRED":WorkflowReports.TranslationReady(options,sources)?"TRANSLATION_COMPLETE":"TRANSLATION_INCOMPLETE"):(options.Translation==null?"TRANSLATION_INCOMPLETE":options.Translation.Status);}}
}
}
