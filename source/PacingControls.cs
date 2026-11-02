using System;using System.Drawing;using System.Windows.Forms;
namespace V15Voice {public partial class MainForm {
 ComboBox punctuationMode=new ComboBox();NumericUpDown commaPause=new NumericUpDown(),stopPause=new NumericUpDown(),questionPause=new NumericUpDown(),paragraphPause=new NumericUpDown();FlowLayoutPanel punctuationSettings=new FlowLayoutPanel();
 void BuildPacingControls(FlowLayoutPanel panel){AddLabel(panel,"03  NGẮT NGHỈ THEO DẤU CÂU",true);punctuationMode.Width=250;punctuationMode.DropDownStyle=ComboBoxStyle.DropDownList;punctuationMode.Items.AddRange(new object[]{"Auto · giọng tự ngắt nghỉ","Tùy chỉnh thời gian nghỉ"});panel.Controls.Add(punctuationMode);
  punctuationSettings.FlowDirection=FlowDirection.TopDown;punctuationSettings.WrapContents=false;punctuationSettings.AutoSize=true;punctuationSettings.Width=250;punctuationSettings.Margin=new Padding(0);panel.Controls.Add(punctuationSettings);
  AddNumber(punctuationSettings,"Dấu phẩy , (giây)",commaPause,0,1.5m,.14m,.01m);AddNumber(punctuationSettings,"Dấu chấm . (giây)",stopPause,0,1.5m,.30m,.01m);AddNumber(punctuationSettings,"Dấu hỏi ? / cảm thán ! (giây)",questionPause,0,1.5m,.35m,.01m);AddNumber(punctuationSettings,"Xuống đoạn trong một khối (giây)",paragraphPause,0,1.5m,.50m,.01m);
  punctuationMode.SelectedIndexChanged+=(s,e)=>{punctuationSettings.Visible=punctuationMode.SelectedIndex==1;if(stateLoaded){UpdateShared();SaveSession();}};punctuationMode.SelectedIndex=0;
  foreach(var number in new[]{commaPause,stopPause,questionPause,paragraphPause})number.ValueChanged+=(s,e)=>{if(stateLoaded){UpdateShared();SaveSession();}};AddLabel(panel,"Dùng chung hàng đợi. Khóa SRT: rút nghỉ trước khi tăng tốc; giữ nguyên mốc. Hơi thở cài riêng.",false);
 }
 RenderOptions PacingOptions(){return new RenderOptions{Punctuation=(PunctuationMode)Math.Max(0,punctuationMode.SelectedIndex),CommaPause=(double)commaPause.Value,StopPause=(double)stopPause.Value,QuestionPause=(double)questionPause.Value,ParagraphPause=(double)paragraphPause.Value};}
 void AddPacing(RenderOptions o){var p=PacingOptions();o.Punctuation=p.Punctuation;o.CommaPause=p.CommaPause;o.StopPause=p.StopPause;o.QuestionPause=p.QuestionPause;o.ParagraphPause=p.ParagraphPause;Punctuation.Validate(o);}
}}
