using System;using System.IO;using System.Linq;using System.Drawing;using System.Windows.Forms;using System.Threading.Tasks;using System.Collections.Generic;
namespace V15Voice {public partial class MainForm {
 ComboBox workflowStudio,workflowBatch;Label workflowStudioHint,workflowBatchHint;bool workflowSync;
 void RefreshWorkflowStop(){if(globalCancel==null)return;bool primary=tabs.SelectedIndex==0||tabs.SelectedTab==batchPage;globalCancel.Visible=!primary;var foot=globalCancel.Parent as TableLayoutPanel;if(foot!=null)foot.ColumnStyles[1].Width=primary?0:125;}
 int WorkflowChoice{get{return workflowStudio!=null&&workflowStudio.SelectedIndex==1?2:1;}}
 Control WorkflowSelector(bool queued){
  if(queued)return BeoWorkflowSelector();
  var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
  panel.RowStyles.Add(new RowStyle(SizeType.Absolute,22));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,36));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  panel.Controls.Add(new Label{Text="CHẾ ĐỘ CHẠY",Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=navy,Dock=DockStyle.Fill},0,0);
  var pick=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,Font=new Font("Segoe UI",11),Name=queued?"workflowBatch":"workflowStudio",AccessibleName="Hai chế độ chạy tự động"};
  pick.Items.AddRange(new object[]{"1 · Chạy theo file — không tự sửa","2 · AI tự sửa — hoàn toàn tự động"});
  var hint=new Label{Dock=DockStyle.Fill,ForeColor=muted,Font=new Font("Segoe UI",9),Padding=new Padding(0,3,0,0)};panel.Controls.Add(pick,0,1);panel.Controls.Add(hint,0,2);
  if(queued){workflowBatch=pick;workflowBatchHint=hint;}else{workflowStudio=pick;workflowStudioHint=hint;}
  extraControls.Add(pick);pick.SelectedIndexChanged+=(s,e)=>{if(!workflowSync)ApplyWorkflowChoice(pick.SelectedIndex+1);};pick.SelectedIndex=0;RefreshWorkflowHints();return panel;
 }
 void BuildWorkflowStudio(){
  var left=(TableLayoutPanel)grid.Parent;left.Controls.Remove(info);left.RowStyles[1].Height=132;
  var host=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};host.RowStyles.Add(new RowStyle(SizeType.Percent,100));host.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
  host.Controls.Add(WorkflowSelector(false),0,0);info.Font=new Font("Segoe UI",9);host.Controls.Add(info,0,1);left.Controls.Add(host,0,1);
  foreach(string col in new[]{"text","start","end"})grid.Columns[col].ReadOnly=true;
  grid.Columns["text"].HeaderText="Lời kịch bản";grid.SizeChanged+=(s,e)=>ResizeWorkflowGrid();mode.SelectedIndexChanged+=(s,e)=>ResizeWorkflowGrid();voiceMode.SelectedIndexChanged+=(s,e)=>ResizeWorkflowGrid();ResizeWorkflowGrid();
 }
 void ResizeWorkflowGrid(){bool small=grid.ClientSize.Width<740;grid.Columns["words"].Visible=!small&&!AnchoredMode;grid.Columns["id"].Visible=grid.ClientSize.Width>=600;grid.Columns["start"].Width=grid.Columns["end"].Width=small?90:100;grid.Columns["qa"].Width=small?110:150;grid.Columns["text"].MinimumWidth=small?120:160;grid.Columns["anchor"].Width=small?65:90;if(grid.Columns.Contains("speaker"))grid.Columns["speaker"].Width=small?52:70;}
 void ApplyWorkflowChoice(int selected){
  if(workflowSync)return;workflowSync=true;try{int index=selected==2?1:0;if(workflowStudio!=null)workflowStudio.SelectedIndex=index;if(workflowBatch!=null)workflowBatch.SelectedIndex=index;if(aiMode.Items.Count>=3)aiMode.SelectedIndex=index==1?2:0;aiFixShort.Checked=true;pauseBatchReview.Checked=false;RefreshWorkflowHints();RefreshBeoModes();}finally{workflowSync=false;}
  if(stateLoaded&&!restoring){if(selected==1&&!busy&&!String.IsNullOrEmpty(sourcePath))try{cues=WorkflowRunner.SourceWords(sourcePath,cues);Populate();}catch(Exception ex){status.Text=ex.Message;}UpdateShared();SaveSession();}
 }
 void RefreshWorkflowHints(){
  string text=WorkflowChoice==1?"Giữ nguyên lời. Kết thúc mỗi file: đúng 1 báo cáo, dù đạt hay lỗi.":"Tự đo, sửa và chạy tiếp. Mỗi file: 2 báo cáo — trước/sau và câu đã sửa Anh–Việt.";
  if(workflowStudioHint!=null)workflowStudioHint.Text=text;if(workflowBatchHint!=null)workflowBatchHint.Text=text;
 }
 void BuildWorkflowTools(FlowLayoutPanel parent){
  // Legacy fields remain for saved settings/cache compatibility; there is no
  // third user-facing 'approve' mode and no repair dialog in the run path.
  aiMode.Items.AddRange(new object[]{"Tắt","Duyệt cũ","Tự động"});aiMode.SelectedIndex=0;aiFixShort.Checked=true;
  AddLabel(parent,"AI OFFLINE CHO CHẾ ĐỘ 2",true);
  parent.Controls.Add(Btn("Cài / kiểm tra gói AI offline",async(s,e)=>{if(busy)return;active=new KokoroEngine{Root=root};var owner=active;try{SetBusy(true);await Task.Run(()=>LocalAi.Setup(root,()=>owner.Cancelled,m=>{if(!IsDisposed)BeginInvoke((Action)(()=>status.Text=m));}));status.Text="Gói AI đã sẵn sàng cho chế độ 2.";}catch(OperationCanceledException){status.Text="Đã dừng cài AI.";}catch(Exception ex){status.Text="Chưa cài được AI: "+ex.Message;}finally{owner.Dispose();active=null;SetBusy(false);}}));
  AddLabel(parent,"Chế độ 1 không gọi AI. Chế độ 2 chỉ tự nhận lời đã đo đạt và qua kiểm tra nghĩa; phần chưa đạt được ghi vào báo cáo, không chờ nút duyệt.",false);
  if(!beoBatchReady){if(batchMode.Parent!=null)batchMode.Parent.Controls.Remove(batchMode);AddLabel(parent,"QUY TẮC TIMELINE CỦA HÀNG ĐỢI",true);batchMode.Width=320;parent.Controls.Add(batchMode);extraControls.Add(batchMode);}

 }
 void SetWorkflowJob(BatchJob job,WorkflowResult result,RenderOptions options){
  if(result.SourceHash.Length>0)job.SourceHash=result.SourceHash;job.Cues=VoiceRenderReport.Snapshot(result.Cues);job.OutputPath=result.Bundle.Wave;job.ReportPath=result.Bundle.Primary;job.ShortReportPath=result.Bundle.Changes;job.WorkflowMode=options.WorkflowMode;
  job.RepairSession=options.AiRepair;job.AiReport=options.AiRepair==null?"":options.AiRepair.Report;job.AiSnapshot=options.AiRepair==null?"":AiRepairSession.SnapshotHash(result.Cues);job.RepairMode=options.Anchored?2:options.Locked?0:-1;
  job.FailureCode=result.Status=="FINAL_VERIFIED"?"":result.Status;job.StackTrace=result.Error==null?"":result.Error.ToString();job.Error=result.Rendered?"":result.Error==null?result.Status:result.Error.Message;job.Warning=result.Status=="TRANSLATION_INCOMPLETE"?"Bản Việt của câu đã sửa chưa đủ; xem báo cáo ngắn.":"";
  job.State=result.Status=="FINAL_VERIFIED"?JobState.Done:result.Status=="TRANSLATION_INCOMPLETE"?JobState.TranslationIncomplete:result.Status=="NEEDS_REVIEW"?JobState.NeedsReview:result.Status=="CANCELLED"?JobState.Cancelled:JobState.Failed;
  if(result.Rendered){job.AudioDuration=FinalAudio.PcmFrames(result.Bundle.Wave)/24000.0;job.DurationFinal=true;}else{job.AudioDuration=0;job.DurationFinal=false;}
 }
 WorkflowResult WorkflowPreflightFailure(string requested,List<Cue> input,Exception error,BatchJob job=null){
  var o=ReportSettings();o.WorkflowMode=WorkflowChoice;o.ReportContext=ReportContext(job==null?sourcePath:job.InputPath,input,job);var engine=active??new KokoroEngine{Root=root};bool owned=active==null;
  try{var r=WorkflowRunner.Run(input,engine,o,requested,WorkflowChoice,null,(n,m)=>{},error);if(job!=null)SetWorkflowJob(job,r,o);lastVoiceReport=r.Bundle.Primary;return r;}finally{if(owned)engine.Dispose();}
 }
 async Task RunWorkflowStudio(){
  RenderOptions o=null;string requested=String.IsNullOrEmpty(sourcePath)?"":OutputFiles.Beside(sourcePath);
  try{
   ReadGrid();if(requested.Length==0)using(var d=new SaveFileDialog{Filter="WAV|*.wav",FileName="kich_ban.wav",Title="Nơi lưu kết quả",OverwritePrompt=false}){if(d.ShowDialog()!=DialogResult.OK)return;requested=d.FileName;}
   o=Options();o.WorkflowMode=WorkflowChoice;o.ReportContext=ReportContext(sourcePath,cues,editingJob);var settings=CaptureSettings();var prepare=WorkflowChoice==2?AutoAiPreparation(settings):null;o.Translator=WorkflowChoice==2?new LocalAi(root,settings.Device):null;
   active=RunEngine(cues);var owner=active;SetBusy(true);if(player!=null)player.Stop();progress.Value=0;
   beoStudioProgress.Begin();var result=await Task.Run(()=>WorkflowRunner.Run(cues,owner,o,requested,o.WorkflowMode,prepare,(n,m)=>{beoStudioProgress.Update(n,m);if(!IsDisposed)BeginInvoke((Action)(()=>{progress.Value=Math.Max(0,Math.Min(100,n));status.Text=m;}));}));
   beoStudioProgress.Finish(result.Status=="FINAL_VERIFIED");cues=result.Cues;lastVoiceReport=result.Bundle.Primary;lastOutput=result.Bundle.Wave;pendingAi=o.AiRepair;pendingAiSnapshot=AiRepairSession.SnapshotHash(cues);
   if(editingJob!=null)SetWorkflowJob(editingJob,result,o);openOutput.Enabled=File.Exists(result.Bundle.Primary);
   if(result.Rendered)waveform=new FileInfo(lastOutput).Length<=32*1024*1024?Wave.Read(lastOutput):null;
   else waveform=null;wavePanel.Invalidate();status.Text=result.Status+" · "+result.Bundle.Reports.Length+" báo cáo · "+Path.GetFileName(result.Bundle.Primary);
  }catch(Exception error){try{if(requested.Length>0){var r=WorkflowPreflightFailure(requested,cues,error,editingJob);status.Text=r.Status+" · báo cáo: "+r.Bundle.Primary;}else status.Text=error.Message;}catch(Exception writeError){status.Text="Không xuất được báo cáo: "+writeError.Message+" · lỗi tác vụ: "+error.Message;}}
  finally{if(active!=null)active.Dispose();active=null;SetBusy(false);CaptureTargetResult(o);Populate();RefreshJobs();SaveSession();}
 }
 async Task RunWorkflowBatch(){
  try{
   var selectedJobs=jobs.Where(j=>j.Selected).ToList();if(selectedJobs.Count==0){status.Text="Thêm/chọn file SRT/TXT trước khi chạy.";return;}beoActiveJobs=selectedJobs;
   foreach(var old in selectedJobs.Where(j=>j.State==JobState.Done&&j.WorkflowMode!=WorkflowChoice)){old.State=JobState.Queued;old.Progress.Restore(0,0,false);}
   var o=OptionsForRun(false);o.WorkflowMode=WorkflowChoice;var settings=CaptureSettings();var prepare=WorkflowChoice==2?AutoAiPreparation(settings):null;o.Translator=WorkflowChoice==2?new LocalAi(root,settings.Device):null;
   active=RunEngine(selectedJobs.Where(j=>j.State!=JobState.ImportFailed).SelectMany(j=>j.Cues).ToList());var owner=active;bool skip=skipFinished.Checked;int chosenTimeline=batchMode.SelectedIndex,selectedTimeline=mode.SelectedIndex;
   var contexts=jobs.ToDictionary(j=>j,j=>ReportContext(j.InputPath,j.Cues,j));SetBusy(true);if(player!=null)player.Stop();
   // Invalid inputs also receive exactly the selected report set.
   foreach(var bad in selectedJobs.Where(j=>j.State==JobState.ImportFailed).ToList()){WorkflowPreflightFailure(OutputFiles.Beside(bad.InputPath),bad.Cues,new IOException("INPUT_ERROR: "+bad.Error),bad);bad.State=JobState.ImportFailed;}
   lastOutput=await Task.Run(()=>BatchRunner.Run(selectedJobs,skip,()=>owner.Cancelled,(job,path,notify)=>{
    if(job.FailureCode=="INPUT_ERROR"&&job.Cues.Count==0){job.State=JobState.Failed;return "";}
    AnchoredVoice.ConfigureBatch(o,job.Timed,chosenTimeline,selectedTimeline);o.TargetSeconds=job.Timed?0:job.TargetSeconds;o.TargetRaw=o.TargetSeconds>0?Scripts.Stamp(o.TargetSeconds):"";o.ReportContext=contexts[job];o.ReadingChanged=r=>{job.AudioDuration=r.AudioSeconds;job.DurationFinal=r.Final;};
    var result=WorkflowRunner.Run(job.Cues,owner,o,path,o.WorkflowMode,prepare,notify);SetWorkflowJob(job,result,o);if(result.Status=="CANCELLED"){job.CheckpointCues=VoiceRenderReport.Snapshot(result.Cues);throw new OperationCanceledException();}return result.Bundle.Wave;
   },(n,m)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{status.Text=m;RefreshJobs();RefreshBeoProgress();}));},false));
   openOutput.Enabled=!String.IsNullOrEmpty(lastOutput)||jobs.Any(j=>File.Exists(j.ReportPath));status.Text=(owner.Cancelled?"Đã dừng":"Đã kết thúc tự động")+" · "+jobs.Count(j=>j.State==JobState.Done)+" đạt · "+jobs.Count(j=>j.State==JobState.NeedsReview)+" chưa sửa đạt · "+jobs.Count(j=>j.State==JobState.Failed||j.State==JobState.ImportFailed)+" lỗi · "+jobs.Count(j=>j.State==JobState.TranslationIncomplete)+" thiếu bản Việt.";
  }catch(Exception error){foreach(var job in jobs.Where(j=>!(skipFinished.Checked&&j.State==JobState.Done)))try{WorkflowPreflightFailure(OutputFiles.Beside(job.InputPath),job.Cues,error,job);}catch{}status.Text="Không chạy được hàng đợi: "+error.Message;}
  finally{if(active!=null)active.Dispose();active=null;SetBusy(false);if(editingJob!=null)cues=editingJob.Cues;Populate();RefreshJobs();SaveSession();}
 }
}}
