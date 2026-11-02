using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace V15Voice {
// Keep the operation at the original cue/stage boundary. Never make a one-cue
// session: neighboring context, punctuation reserves and A/B routing need it.
public static class InlineAiRepair {
 public static bool TimingProblem(Cue c) {
  string s=c.Status??"";
  return !c.Prose && (s.StartsWith("QUÁ",StringComparison.OrdinalIgnoreCase)||s.Contains("Dư ")||s.Contains("Thiếu ")||s.Contains("Cần sửa lời")||s.Contains("NEEDS_REVIEW")||s.Contains("SEMANTIC_REVIEW_REQUIRED")||s.Contains("ENGINE_ERROR")||s.Contains("RENDER_FAILED")||s.Contains("MANUAL REQUIRED")||s.Contains("REVIEW REQUIRED")||s.Contains("ENGINE ERROR")||s.Contains("AUDIO ERROR"));
 }
 public static AiRepairRow At(AiRepairSession p,int id) {
  return p.Rows.FirstOrDefault(r=>r.Stage.Cues.Any(c=>c.Id==id));
 }
 public static bool Matches(AiRepairSession p,List<Cue> cues) {
  return p!=null && AiRepairSession.SnapshotHash(p.Effective())==AiRepairSession.SnapshotHash(cues);
 }
 public static void Apply(AiRepairSession p,List<Cue> cues,int id) {
  p.Validate();var row=At(p,id);if(row==null)throw new Exception("Không tìm thấy câu/chặng cần sửa.");
  var chosen=row.Applied;var words=row.CurrentWords;
  var targets=words.Select(w=>new {Word=w,Cue=cues.FirstOrDefault(c=>c.Id==w.Id)}).ToList();
  if(targets.Any(t=>t.Cue==null||t.Cue.Start!=t.Word.Start||t.Cue.End!=t.Word.End||t.Cue.Speaker!=t.Word.Speaker||t.Cue.AnchorAfter!=t.Word.AnchorAfter))throw new Exception("Cấu trúc kịch bản đã đổi. Đo lại trước khi áp dụng.");
  foreach(var t in targets) {
   t.Cue.Text=t.Word.Text;t.Cue.AudioPath=null;t.Cue.Actual=0;
   var m=chosen==null?row.Original:chosen.Measurement;
   t.Cue.Natural=!p.Anchored&&m!=null?m.Natural:0;t.Cue.Speed=m==null?row.BaseSpeed:m.Speed;
   t.Cue.Status=chosen!=null?(chosen.Manual?"MANUAL_FIXED · ":"AI_FIXED · ")+(p.Anchored?"chặng ":"")+m.After.ToString("0.000")+"s · cần xuất WAV tổng":m==null?"Chưa tạo":m.Error.Length>0?"Lỗi TTS/model · "+m.Error:m.Detail;
  }
 }
}

public partial class MainForm {
 AiRepairSession inlineAiSession;string inlineAiFingerprint="";
 volatile bool inlineAiCancelled;volatile KokoroEngine inlineAiEngine;bool inlineAiRunning,inlineAiFitting;

 void BuildInlineAi() {
  grid.Columns.Add(new DataGridViewTextBoxColumn{Name="aiFix",HeaderText="Sửa ngay",ReadOnly=true,Width=122,SortMode=DataGridViewColumnSortMode.NotSortable});
  grid.Columns["aiFix"].DisplayIndex=grid.Columns["qa"].DisplayIndex+1;
  foreach(DataGridViewColumn c in grid.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
  grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
  grid.CellContentClick+=async(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==grid.Columns["aiFix"].Index&&!busy)await RepairInlineCue(e.RowIndex);};
  grid.SizeChanged+=(s,e)=>ResizeInlineAiColumns();ResizeInlineAiColumns();BuildQuickPanel();
 }
 void ResizeInlineAiColumns() {
  if(!grid.Columns.Contains("aiFix"))return;
  bool compact=grid.ClientSize.Width<820;
  grid.Columns["words"].Visible=!compact;grid.Columns["id"].Visible=grid.ClientSize.Width>=700;
  grid.Columns["start"].Width=grid.Columns["end"].Width=compact?96:100;
  grid.Columns["start"].DefaultCellStyle=new DataGridViewCellStyle{WrapMode=DataGridViewTriState.False};
  grid.Columns["end"].DefaultCellStyle=new DataGridViewCellStyle{WrapMode=DataGridViewTriState.False};
  grid.Columns["text"].DefaultCellStyle=new DataGridViewCellStyle{WrapMode=DataGridViewTriState.True};
  grid.Columns["qa"].DefaultCellStyle=new DataGridViewCellStyle{WrapMode=DataGridViewTriState.True};
  grid.Columns["qa"].Width=compact?110:150;grid.Columns["aiFix"].Width=compact?110:122;
  grid.Columns["text"].MinimumWidth=compact?100:160;
  grid.Columns["anchor"].Width=compact?70:90;
  FitInlineAiRows();
 }
 void FitInlineAiRows() {
  if(inlineAiFitting||!grid.Columns.Contains("aiFix"))return;inlineAiFitting=true;
  try{foreach(DataGridViewRow row in grid.Rows){row.MinimumHeight=44;int height=44;foreach(string name in new[]{"text","qa"}){var cell=row.Cells[name];string value=Convert.ToString(cell.Value);int width=Math.Max(40,grid.Columns[name].Width-14);var size=TextRenderer.MeasureText(value,cell.InheritedStyle.Font??grid.Font,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);height=Math.Max(height,size.Height+14);}row.Height=Math.Min(260,height);row.Cells["text"].ToolTipText=Convert.ToString(row.Cells["text"].Value);}}
  finally{inlineAiFitting=false;}
 }
 void RefreshInlineAi() {
  if(!grid.Columns.Contains("aiFix"))return;
  bool supported=!proseInput&&mode.SelectedIndex!=1;
  bool matches=InlineAiRepair.Matches(inlineAiSession,cues);
  var rows=matches?inlineAiSession.Rows.SelectMany(r=>r.Stage.Cues.Select(c=>new {Id=c.Id,Row=r})).ToDictionary(x=>x.Id,x=>x.Row):new Dictionary<int,AiRepairRow>();
  for(int i=0;i<cues.Count&&i<grid.Rows.Count;i++) {
   grid.Rows[i].MinimumHeight=44;
   var c=cues[i];AiRepairRow row;rows.TryGetValue(c.Id,out row);
   string action="",tip="";
   if(supported&&!busy) {
    if(row!=null&&row.Chosen!=null){action="Hoàn tác";tip="Khôi phục lời trước khi AI sửa cho câu/chặng này.";}
    else if(InlineAiRepair.TimingProblem(c)) {
     action="SỬA NGAY";
     tip=AnchoredMode?"Sửa và đo lại cả chặng chứa câu này; giữ mốc và giọng A/B.":"Sửa và đo lại riêng câu này; tự áp dụng khi đo đạt. Không cần bật AI trong thiết lập.";
     if(action=="Sửa tay")tip="AI đã thử 3 lần chưa đạt. Bấm để sửa trực tiếp ô lời đọc, rồi tạo voice lại.";
     if(action=="Áp dụng lại")tip="Áp dụng lại đề xuất đã đo đạt trong phiên này; giữ mốc thời gian và các câu khác.";
    }
   }
   DataGridViewCell cell=grid.Rows[i].Cells["aiFix"];
   if(action.Length>0&&!(cell is DataGridViewButtonCell))cell=grid.Rows[i].Cells["aiFix"]=new DataGridViewButtonCell{FlatStyle=FlatStyle.Standard};
   else if(action.Length==0&&!(cell is DataGridViewTextBoxCell))cell=grid.Rows[i].Cells["aiFix"]=new DataGridViewTextBoxCell();
   cell.Value=action;cell.ToolTipText=tip;
   if(row!=null&&row.Chosen!=null)grid.Rows[i].DefaultCellStyle.BackColor=Color.FromArgb(225,245,235);
  }
  FitInlineAiRows();
 }
 void CancelInlineAi(){inlineAiCancelled=true;var e=inlineAiEngine;if(e!=null)e.Cancel();}
 bool ShowInlineTimingFailure(Exception error,VoiceRenderReport report) {
  string message=error.Message??"";
  if(proseInput||mode.SelectedIndex==1||!(message.Contains("câu vẫn vượt timeline")||message.StartsWith("Chưa xuất WAV: có chặng ngoài giới hạn căn tốc độ.")))return false;
  if(!cues.Any(InlineAiRepair.TimingProblem))return false;
  if(report!=null)lastVoiceReport=report.PathName;
  status.Text="Chưa xuất WAV: có phần chưa vừa timeline. Bấm SỬA NGAY cạnh dòng lỗi, đo/chấp nhận rồi TIẾP TỤC RENDER.";
  return true;
 }
 void FocusFirstInlineError(){if(busy||!grid.Columns.Contains("aiFix"))return;int i=cues.FindIndex(InlineAiRepair.TimingProblem);if(i>=0&&i<grid.Rows.Count){grid.CurrentCell=grid.Rows[i].Cells["aiFix"];grid.FirstDisplayedScrollingRowIndex=i;}}
 bool InlineSettingsMatch(AiRepairSession p,VoiceSettings settings,string fingerprint) {
  return InlineAiRepair.Matches(p,cues)&&AiRepairSession.SameSettings(p.Settings,settings.Xml().ToString())&&inlineAiFingerprint==fingerprint;
 }
 void InlineAiApplied(AiRepairSession p,int id) {
  p.Validate();p.Save();InlineAiRepair.Apply(p,cues,id);pendingAi=p;pendingAiSnapshot=AiRepairSession.SnapshotHash(cues);lastVoiceReport=p.Report;
  lastOutput=null;openOutput.Enabled=false;waveform=null;wavePanel.Invalidate();readingLive=false;lastAnchorReport=null;
  if(editingJob!=null){editingJob.Cues=cues;editingJob.State=JobState.Queued;editingJob.Error=editingJob.TimelineReport="";editingJob.Warning="Đã sửa tại dòng · cần xuất WAV tổng";editingJob.AiReport=p.Report;editingJob.AiSnapshot=pendingAiSnapshot;RefreshJobs();}
  Populate();SaveSession();
 }
 async Task RepairInlineCue(int index){if(busy||index<0||index>=cues.Count)return;try{ReadGrid();if(proseInput||mode.SelectedIndex==1){status.Text="Sửa tại dòng dùng SRT Khóa hoặc V15.6.";return;}var settings=CaptureSettings();var factory=AiFactory(settings);string fingerprint=AiVoiceFingerprint(settings,factory);if(InlineAiRepair.Matches(inlineAiSession,cues)){var previous=InlineAiRepair.At(inlineAiSession,cues[index].Id);if(previous!=null&&previous.Chosen!=null){previous.Accepted=0;previous.FinalVerified=false;InlineAiApplied(inlineAiSession,cues[index].Id);status.Text="Đã hoàn tác lời sửa tại dòng.";return;}}if(!InlineSettingsMatch(inlineAiSession,settings,fingerprint)){inlineAiSession=AiRepairSession.Open(sourcePath,cues,stateDir,settings,aiFixShort.Checked,fingerprint);inlineAiFingerprint=fingerprint;}ShowQuickRepair(inlineAiSession,cues[index].Id);}catch(Exception ex){status.Text=ex.Message;}await Task.FromResult(0);}
 async Task RunInlineAi(AiRepairSession p,int id,ITextRewriter rewriter,Func<string,KokoroEngine> factory) {
  if(busy)return;
  if(!InlineAiRepair.Matches(p,cues))throw new Exception("Kịch bản đã đổi. Chọn lại câu cần sửa.");
  bool acceptAutomatically=aiMode.SelectedIndex==2;var selectedRow=InlineAiRepair.At(p,id);if(selectedRow==null)throw new Exception("Không tìm thấy câu cần sửa.");int priorAccepted=selectedRow.Accepted;
  inlineAiSession=p;inlineAiRunning=true;inlineAiCancelled=false;SetBusy(true);if(player!=null)player.Stop();progress.Value=0;
  Action<int,string> notify=(n,msg)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{if(inlineAiRunning){progress.Value=Math.Max(0,Math.Min(100,n));status.Text="Câu #"+id+" · "+msg;}}));};
  try {
   if(rewriter is LocalAi&&!LocalAi.Ready(root))await Task.Run(()=>LocalAi.Setup(root,()=>inlineAiCancelled,msg=>notify(0,msg)));
   await Task.Run(()=>AiTimeline.Run(p,rewriter,role=>{inlineAiEngine=factory(role);return inlineAiEngine;},()=>inlineAiCancelled,notify,acceptAutomatically,true,id));
   if(inlineAiCancelled)throw new OperationCanceledException();
   var row=InlineAiRepair.At(p,id);InlineAiApplied(p,id);
   status.Text=row.Chosen!=null?"Đã sửa "+(p.Anchored?"chặng chứa câu #":"câu #")+id+" và đo đạt. Bấm TẠO VOICE · WAV để xuất toàn bài; có thể Hoàn tác tại dòng.":row.Best!=null?"Có đề xuất đã đo đạt. Bấm CHẤP NHẬN rồi TIẾP TỤC RENDER.":row.Original!=null&&row.Original.Error.Length>0?"Lỗi bộ tạo giọng: "+row.Original.Error:row.Original!=null&&!row.Original.NeedsRepair?"Câu #"+id+" đã đo đạt, giữ nguyên lời.":"AI chưa tìm được bản vừa khung. Bấm TỰ SỬA trong khung; nguyên nhân và số đo nằm ngay tại đây.";
  } catch(OperationCanceledException){selectedRow.Accepted=priorAccepted;p.Save();status.Text="Đã dừng sửa câu #"+id+". Lời đang dùng và file nguồn được giữ nguyên.";}
   catch(Exception ex){status.Text="Chưa sửa được câu #"+id+": "+ex.Message;}
  finally {inlineAiEngine=null;inlineAiRunning=false;lastVoiceReport=p.Report;SetBusy(false);Populate();SaveSession();RefreshQuickRepair();}
 }
}
}
