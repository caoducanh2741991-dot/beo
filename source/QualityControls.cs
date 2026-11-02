using System;using System.IO;using System.Linq;using System.Drawing;using System.Windows.Forms;using System.Threading.Tasks;
namespace V15Voice {public partial class MainForm {
 Button rhythmMeasure;ComboBox fitMode=new ComboBox();NumericUpDown minSpeed=new NumericUpDown(),targetLufs=new NumericUpDown();CheckBox normalizeLoudness=new CheckBox();FlowLayoutPanel qualityControls=new FlowLayoutPanel();readonly SpeechCache measuredSpeech=new SpeechCache();
 void BuildQualityControls(FlowLayoutPanel parent){
  qualityControls.FlowDirection=FlowDirection.TopDown;qualityControls.WrapContents=false;qualityControls.AutoSize=true;qualityControls.Width=250;qualityControls.Margin=new Padding(0);parent.Controls.Add(qualityControls);extraControls.Add(qualityControls);
  AddLabel(qualityControls,"Căn theo thời lượng thực của giọng",false);fitMode.Width=250;fitMode.DropDownStyle=ComboBoxStyle.DropDownList;fitMode.Items.AddRange(new object[]{"Nhẹ ±10% · ưu tiên tự nhiên","Dùng giới hạn tốc độ đã đặt"});qualityControls.Controls.Add(fitMode);
  AddNumber(qualityControls,"Tốc độ tối thiểu khi căn",minSpeed,.5m,1.3m,.9m,.01m);
  rhythmMeasure=Btn("Đo nhịp giọng · 5 khối",async(s,e)=>await MeasureRhythm());qualityControls.Controls.Add(rhythmMeasure);
  AddLabel(qualityControls,"Câu quá ngắn sẽ được báo nghỉ dư; không kéo giãn mạnh hoặc tự sửa SRT.",false);
  AddLabel(qualityControls,"CÂN ÂM LƯỢNG WAV",true);normalizeLoudness.Text="Tự cân âm lượng khi xuất WAV";normalizeLoudness.Checked=true;normalizeLoudness.AutoSize=true;normalizeLoudness.MaximumSize=new Size(250,0);qualityControls.Controls.Add(normalizeLoudness);
  AddNumber(qualityControls,"Âm lượng đích (LUFS)",targetLufs,-24,-14,-18,1);targetLufs.DecimalPlaces=0;AddLabel(qualityControls,"Dùng chung Tạo voice / Hàng đợi. Giới hạn đỉnh -1 dBTP; giữ nguyên độ dài WAV.",false);
  fitMode.SelectedIndexChanged+=(s,e)=>{RefreshQualityEnabled();if(stateLoaded&&!restoring){UpdateShared();SaveSession();}};fitMode.SelectedIndex=0;
  normalizeLoudness.CheckedChanged+=(s,e)=>{RefreshQualityEnabled();if(stateLoaded&&!restoring){UpdateShared();SaveSession();}};
  foreach(var n in new[]{minSpeed,targetLufs})n.ValueChanged+=(s,e)=>{if(stateLoaded&&!restoring){UpdateShared();SaveSession();}};
  speed.ValueChanged+=(s,e)=>{minSpeed.Maximum=speed.Value;if(minSpeed.Value>speed.Value)minSpeed.Value=speed.Value;};RefreshQualityEnabled();
 }
 void RefreshQualityEnabled(){bool custom=fitMode.SelectedIndex==1,timed=!proseInput||textTargetEnabled.Checked;maxSpeed.Enabled=!busy&&timed&&custom;minSpeed.Enabled=!busy&&timed&&custom;fitMode.Enabled=!busy&&timed;slow.Enabled=!busy&&!proseInput;mode.Enabled=!busy&&!proseInput;targetLufs.Enabled=!busy&&normalizeLoudness.Checked;RefreshAnchorControls();}
 void AddQuality(RenderOptions o){o.GentleFit=fitMode.SelectedIndex==0;o.MinSpeed=(double)minSpeed.Value;o.NormalizeLoudness=normalizeLoudness.Checked;o.TargetLufs=(double)targetLufs.Value;Tempo.Validate(o);FinalAudio.Validate(o);}
 Wave FinishPreview(Wave wave,string path,RenderOptions o){wave.Write(path);if(!o.NormalizeLoudness)return wave;string normalized=path+".normalized.wav";try{FinalAudio.Normalize(root,path,normalized,wave.Samples.Length,o.TargetLufs,()=>active!=null&&active.Cancelled,ShowActivity);File.Copy(normalized,path,true);return Wave.Read(path);}finally{try{File.Delete(normalized);}catch{}}}
 async Task MeasureRhythm(){if(AnchoredMode){await MeasureAnchored();return;}if(proseInput){await MeasureProse();return;}string work=Path.Combine(Path.GetTempPath(),"V15_rhythm_"+Guid.NewGuid().ToString("N"));try{
  ReadGrid();if(cues.Count==0)throw new Exception("Mở SRT trước khi đo nhịp.");var o=PacingOptions();o.BaseSpeed=(double)speed.Value;o.MaxSpeed=(double)maxSpeed.Value;o.SlowShort=slow.Checked;AddQuality(o);active=RunEngine(cues);active.CheckFiles();SetBusy(true);var engine=active;var selected=cues.Select(c=>new Cue{Id=c.Id,Start=c.Start,End=c.End,Text=c.Text,Speaker=c.Speaker}).ToList();
  string report=await Task.Run(()=>VoiceRhythm.Measure(selected,engine,o,work,(p,m)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{progress.Value=p;status.Text=m;}));}));active.Dispose();active=null;SetBusy(false);
  using(var f=new Form{Text="Nhịp giọng · 5 khối đại diện",Size=new Size(850,500),StartPosition=FormStartPosition.CenterParent}){f.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=report,Font=new Font("Segoe UI",11)});f.ShowDialog(this);}
 }catch(OperationCanceledException){status.Text="Đã dừng đo nhịp giọng.";}catch(Exception e){Error(e);}finally{if(active!=null)active.Dispose();active=null;SetBusy(false);try{Directory.Delete(work,true);}catch{}}}
}}
