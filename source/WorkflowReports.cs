using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace V15Voice {
// WorkflowMode 0 is retained only for old internal integrations. The app exposes
// exactly two choices: 1 = file as supplied; 2 = unattended, validated AI repair.
public sealed class WorkflowBundle {
 public string Wave="",Srt="",Primary="",Changes="",Status="";
 public string[] Reports {get{return String.IsNullOrEmpty(Changes)?new[]{Primary}:new[]{Primary,Changes};}}
}
public static class WorkflowReports {
 static readonly string[] occupied={".wav",".final.srt",".aligned.srt",".voice-report.txt",".ai-before-after-report.txt",".ai-changes-en-vi.txt",".ai-repair-report.txt",".final.vi.txt"};
 static string F(double n){return n.ToString("0.000",CultureInfo.InvariantCulture);}
 static string M(AiMeasurement m){return m==null?"CHƯA ĐO":m.Error.Length>0?m.Kind+": "+m.Error:"TTS gồm nghỉ tự nhiên "+F(m.Tts)+"s; nghỉ app "+F(m.AppPauses)+"s; hơi thở "+F(m.Breaths)+"s; tổng "+F(m.Natural)+"s; sau căn "+F(m.After)+"s; đích "+F(m.Target)+"s; chênh "+F(m.Delta)+"s; tốc độ "+F(m.Speed)+"x"+(m.PauseNote.Length==0?"":"; cân nghỉ: "+m.PauseNote)+(m.BeforePauseNatural>0?"; trước cân nghỉ: tổng "+F(m.BeforePauseNatural)+"s, sau tốc độ "+F(m.BeforePauseAfter)+"s":"");}
 public static List<Cue> Changed(RenderOptions o,List<Cue> final){
  var original=(o.WorkflowOriginal??final).ToDictionary(c=>c.Id);
  return final.Where(c=>original.ContainsKey(c.Id)&&original[c.Id].Text!=c.Text).Select(c=>VoiceRenderReport.Snapshot(new[]{c})[0]).ToList();
 }
 public static TranslationResult Translate(RenderOptions o,List<Cue> final,Func<bool> cancel,Action<int,string> progress){
  if(o.WorkflowMode!=2)return null;var changed=Changed(o,final);if(changed.Count==0)return null;
  return OfflineTranslation.Run(changed,o.Translator,String.IsNullOrEmpty(o.TranslationCacheRoot)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"V15VoiceStudio","translation-cache"):o.TranslationCacheRoot,cancel,progress,o.ReportContext==null?"":o.ReportContext.SourcePath);
 }
 public static bool TranslationReady(RenderOptions o,List<Cue> final){
  var changed=Changed(o,final);if(o.WorkflowMode!=2||changed.Count==0)return true;
  var t=o.Translation;if(t==null||!t.Complete||t.SourceHash!=AiRepairSession.SnapshotHash(changed)||t.Text.Count!=changed.Count)return false;
  try{foreach(var c in changed){string vi;if(!t.Text.TryGetValue(c.Id,out vi))return false;OfflineTranslation.Validate(c.Text,vi);}return true;}catch{return false;}
 }
 public static string Outcome(RenderOptions o,List<Cue> final,bool wave,string failure){
  if(!wave)return String.IsNullOrEmpty(failure)?"RENDER_FAILED":failure;
  return TranslationReady(o,final)?"FINAL_VERIFIED":"TRANSLATION_INCOMPLETE";
 }
 static string Source(RenderOptions o){return o.ReportContext==null?"":o.ReportContext.SourcePath;}
 static string Header(RenderOptions o,string status,bool wave){return "V15 VOICE STUDIO 3.0.0\r\nCHẾ ĐỘ "+o.WorkflowMode+(o.WorkflowMode==1?" — CHẠY THEO FILE, KHÔNG SỬA LỜI":" — AI TỰ ĐỘNG SỬA")+"\r\nNguồn: "+Source(o)+"\r\nKết quả công việc: "+status+"\r\nWAV: "+(wave?"FINAL_VERIFIED — đã tạo và kiểm tra":"CHƯA XUẤT — không có WAV tổng đạt của lần này")+"\r\n";}
 static string Detailed(RenderOptions o,List<Cue> final,VoiceRenderReport report,string status,bool wave,Exception error){
  var b=new StringBuilder(Header(o,status,wave));var changed=Changed(o,final);
  b.AppendLine("Số câu thay đổi lời thực sự áp dụng: "+changed.Count);
  b.AppendLine(o.WorkflowMode==1?"Giữ nguyên lời file đầu vào. Không gọi AI sửa hoặc dịch.":"Chạy và kết thúc tự động; phương án chưa đo đạt hoặc chưa đủ chắc về nghĩa không được tự áp dụng.");
  if(o.WorkflowMode==2&&(o.Prose||(!o.Locked&&!o.Anchored)))b.AppendLine("Phạm vi: AI sửa lời theo cue/chặng dùng cho SRT khóa hoặc V15.6. TXT tự nhiên/SRT linh hoạt giữ lời; TXT có đích vẫn căn tốc độ nhưng chưa tự viết lại lời.");
  if(o.WorkflowMode==2)b.AppendLine(changed.Count==0?"Đối chiếu Việt: không cần dịch vì không có câu đã sửa.":"Đối chiếu Việt của câu đã sửa: "+(TranslationReady(o,final)?"ĐẦY ĐỦ":"TRANSLATION_INCOMPLETE — "+(o.Translation==null?"chưa dịch":o.Translation.Error)));
  b.AppendLine("Mốc cue dưới đây là timeline nguồn. Không suy ra mốc audio từng cue từ số đo toàn chặng.");
  if(o.AiRepair!=null&&o.WorkflowMode==2){
   b.AppendLine("Checkpoint: chặng đang/cuối xử lý "+o.AiRepair.ActiveStage+" · bước "+o.AiRepair.ActivePhase);b.AppendLine("Số chặng có số đo: "+o.AiRepair.Rows.Count(r=>r.CurrentMeasure!=null)+"/"+o.AiRepair.Rows.Count);
   foreach(var row in o.AiRepair.Rows){
    b.AppendLine();b.AppendLine("CHẶNG "+row.Stage.Number+" | cue "+String.Join(",",row.Stage.Cues.Select(c=>c.Id))+" | "+Scripts.Stamp(row.Stage.Start)+" --> "+Scripts.Stamp(row.Stage.End));
    b.AppendLine("TRƯỚC: "+M(row.Original));b.AppendLine("SAU/ĐANG DÙNG: "+M(row.CurrentMeasure));b.AppendLine("Trạng thái: "+row.Status);
    foreach(var c in row.Stage.Cues)b.AppendLine("GỐC #"+c.Id+": "+c.Text);
    foreach(var c in row.CurrentWords)b.AppendLine("ĐANG DÙNG #"+c.Id+": "+c.Text);
    foreach(var a in row.Attempts){b.AppendLine("Thử "+a.Number+" | "+a.Strategy+" | "+a.Code+" | "+(row.Accepted==a.Number?"ĐÃ ÁP DỤNG":"KHÔNG ÁP DỤNG"));b.AppendLine(a.SemanticReason);foreach(var c in a.Cues)b.AppendLine("Đề xuất #"+c.Id+": "+c.Text);b.AppendLine(M(a.Measurement));if(a.Error.Length>0)b.AppendLine(a.Error);if(a.Stack.Length>0)b.AppendLine(a.Stack);}
    if(row.Error.Length>0)b.AppendLine(row.Error);
   }
  }else{
   b.AppendLine();b.AppendLine(o.WorkflowMode==1?"LỜI GIỮ NGUYÊN THEO FILE":"LỜI CUỐI — KHÔNG CẦN AI SỬA");
   foreach(var c in final)b.AppendLine("#"+c.Id+" | "+Scripts.Stamp(c.Start)+" --> "+Scripts.Stamp(c.End)+" | "+c.Text);
  }
  if(error!=null){b.AppendLine();b.AppendLine("LỖI GỐC / STACK TRACE");b.AppendLine(error.ToString());}
  if(report!=null){b.AppendLine();b.AppendLine("SỐ ĐO, THIẾT BỊ, CACHE VÀ CHẨN ĐOÁN TTS");b.AppendLine(report.Build().Replace("SỬA NGAY","xem câu lỗi trong báo cáo"));}
  return b.ToString();
 }
 static string Short(RenderOptions o,List<Cue> final,string status,bool wave){
  var changed=Changed(o,final);var originals=(o.WorkflowOriginal??final).ToDictionary(c=>c.Id);var b=new StringBuilder(Header(o,status,wave));
  b.AppendLine("KIỂM TRA NHANH — CHỈ CÂU THỰC SỰ ĐÃ SỬA: "+changed.Count);
  if(!wave&&changed.Count>0)b.AppendLine("Các câu dưới đây đã áp dụng/đo trong chặng lưu; CHƯA có WAV tổng của lần này.");
  if(changed.Count==0){b.AppendLine("Không có câu nào đã được AI sửa và áp dụng. Đề xuất bị loại/chưa đạt nằm trong báo cáo trước–sau.");return b.ToString();}
  foreach(var c in changed){
   b.AppendLine();b.AppendLine("#"+c.Id+" | "+Scripts.Stamp(c.Start)+" --> "+Scripts.Stamp(c.End));
   b.AppendLine("EN GỐC: "+originals[c.Id].Text);b.AppendLine("EN ĐÃ DÙNG: "+c.Text);
   string vi=null;if(o.Translation!=null)o.Translation.Text.TryGetValue(c.Id,out vi);if(!String.IsNullOrEmpty(vi))try{OfflineTranslation.Validate(c.Text,vi);}catch{vi=null;}
   b.AppendLine("VI ĐỐI CHIẾU: "+(String.IsNullOrWhiteSpace(vi)?"TRANSLATION_INCOMPLETE — chưa có bản dịch được xác nhận":vi));
  }
  b.AppendLine();b.AppendLine("Bản Việt dùng để đối chiếu nhanh; cần đọc lại thuật ngữ và sắc thái. Chưa có forced alignment riêng từng cue.");return b.ToString();
 }
 public static WorkflowBundle Publish(string raw,string requested,List<Cue> final,RenderOptions o,VoiceRenderReport report,string failure="",Exception error=null,Action<int> fault=null){
  bool wave=!String.IsNullOrEmpty(raw);if(o.WorkflowMode!=1&&o.WorkflowMode!=2)throw new InvalidOperationException("Chọn một trong hai chế độ chạy.");
  if(wave){if(!String.IsNullOrEmpty(o.WorkflowSourceHash)&&StateFiles.Hash(Source(o))!=o.WorkflowSourceHash)throw new IOException("SOURCE_CHANGED: file gốc thay đổi trong lúc chạy.");StableArtifacts.VerifyWords(final,report);report.VerifySourceUnchanged();if(o.AiRepair!=null)o.AiRepair.Validate();
   if(o.WorkflowMode==1&&!SameWords(o.WorkflowOriginal??final,final))throw new IOException("SOURCE_WORDS_CHANGED: chế độ 1 không được thay lời.");
  }
  string status=Outcome(o,final,wave,failure),dir=Path.GetDirectoryName(Path.GetFullPath(requested)),basis=Path.GetFileNameWithoutExtension(requested);Directory.CreateDirectory(dir);
  for(int n=0;n<10000;n++){
   string stem=Path.Combine(dir,basis+(n==0?"":" ("+(n+1)+")"));if(occupied.Any(e=>File.Exists(stem+e)||Directory.Exists(stem+e)))continue;
   string lockPath=stem+".v15-publish300.lock";FileStream gate;try{gate=new FileStream(lockPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);}catch(IOException){continue;}
   var created=new List<string>();string work=Path.Combine(dir,".v15_reports300_"+Guid.NewGuid().ToString("N"));
   try{using(gate){if(occupied.Any(e=>File.Exists(stem+e)||Directory.Exists(stem+e)))continue;Directory.CreateDirectory(work);
    var bundle=new WorkflowBundle{Primary=stem+(o.WorkflowMode==1?".voice-report.txt":".ai-before-after-report.txt"),Changes=o.WorkflowMode==2?stem+".ai-changes-en-vi.txt":"",Status=status,Wave=wave?stem+".wav":"",Srt=wave?stem+".final.srt":""};
    if(report!=null){if(wave)report.Finish("THÀNH CÔNG",bundle.Wave,null);report.Outcome=status;if((!wave&&report.FailureCode.Length==0)||status=="TRANSLATION_INCOMPLETE")report.FailureCode=status;}
    if(o.AiRepair!=null){if(wave)o.AiRepair.Reconcile(final,report);o.AiRepair.Output=bundle.Wave;o.AiRepair.Outcome=status;o.AiRepair.TranslationStatus=Changed(o,final).Count==0?"NOT_REQUIRED":TranslationReady(o,final)?"TRANSLATION_COMPLETE":"TRANSLATION_INCOMPLETE";}
    var paths=new List<string>();var utf8=new UTF8Encoding(false);
    string detailTemp=Path.Combine(work,"detail.txt");File.WriteAllText(detailTemp,Detailed(o,final,report,status,wave,error),utf8);paths.Add(detailTemp);
    if(o.WorkflowMode==2){string shortTemp=Path.Combine(work,"changes.txt");File.WriteAllText(shortTemp,Short(o,final,status,wave),utf8);paths.Add(shortTemp);}
    var targets=new List<string>(bundle.Reports);
    if(wave){var srt=Scripts.ToSrt(StableArtifacts.FinalCues(final,o,FinalAudio.PcmFrames(raw)/24000.0));
     var parsed=Scripts.ParseSrt(srt);if(!parsed.Select(c=>c.Text).SequenceEqual(StableArtifacts.FinalCues(final,o,FinalAudio.PcmFrames(raw)/24000.0).Select(c=>c.Text)))throw new IOException("SRT không khớp lời TTS.");
     string srtTemp=Path.Combine(work,"final.srt");File.WriteAllText(srtTemp,srt,utf8);paths.Add(srtTemp);targets.Add(bundle.Srt);
     if(o.AnchorResult!=null&&!String.IsNullOrEmpty(o.AnchorResult.Srt)&&File.Exists(o.AnchorResult.Srt)){string aligned=Path.Combine(work,"aligned.srt");File.Copy(o.AnchorResult.Srt,aligned);paths.Add(aligned);targets.Add(stem+".aligned.srt");}
     string wavTemp=Path.Combine(work,"voice.wav");File.Copy(raw,wavTemp);paths.Add(wavTemp);targets.Add(bundle.Wave);
    }
    for(int i=0;i<paths.Count;i++){File.Move(paths[i],targets[i]);created.Add(targets[i]);if(fault!=null)fault(created.Count);}
    o.WorkflowBundle=bundle;if(o.AiRepair!=null)o.AiRepair.Save();if(report!=null){report.PathName=bundle.Primary;report.ArtifactPublished();}
    if(o.AnchorResult!=null&&wave)o.AnchorResult.Wav=bundle.Wave;if(o.TargetResult!=null&&wave)o.TargetResult.Wav=bundle.Wave;return bundle;
   }}catch{foreach(string p in created)try{File.Delete(p);}catch{}throw;}finally{try{if(Directory.Exists(work))Directory.Delete(work,true);}catch{}try{File.Delete(lockPath);}catch{}}
  }
  throw new IOException("Không chọn được tên bộ kết quả.");
 }
 public static bool SameWords(List<Cue> a,List<Cue> b){return a.Count==b.Count&&a.Zip(b,(x,y)=>x.Id==y.Id&&x.Text==y.Text).All(x=>x);}
}
}
