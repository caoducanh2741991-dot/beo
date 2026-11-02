using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Globalization;
using System.Media;
using System.Diagnostics;
using System.Reflection;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.5", FrameworkDisplayName=".NET Framework 4.5")]
[assembly: AssemblyVersion("1.4.4.0")]
[assembly: AssemblyFileVersion("1.4.4.0")]
namespace V15Voice {
public class VoiceItem {
 public int Id; public string Name,Key; public override string ToString(){return Name;}
 public static readonly string[] Names={"Alloy","Aoede","Bella","Heart","Jessica","Kore","Nicole","Nova","River","Sarah","Sky","Adam","Echo","Eric","Fenrir","Liam","Michael","Onyx","Puck","Santa"};
 public static string Label(int i){return Names[i]+" · "+(i<11?"Nữ":"Nam")+" · US";}
}
public partial class MainForm:Form {
 Color navy=Color.FromArgb(19,31,52),teal=Color.FromArgb(0,119,112),muted=Color.FromArgb(100,116,139);
 DataGridView grid=new DataGridView();ComboBox family=new ComboBox(),voices=new ComboBox(),mode=new ComboBox(),styleFilter=new ComboBox();
 NumericUpDown speed=new NumericUpDown(),maxSpeed=new NumericUpDown(),gap=new NumericUpDown();
 CheckBox slow=new CheckBox();ComboBox breathMode=new ComboBox();TextBox breathPath=new TextBox();NumericUpDown breathVolume=new NumericUpDown(),breathEvery=new NumericUpDown();FlowLayoutPanel breathSettings=new FlowLayoutPanel();
 Label info=new Label(),status=new Label(),voiceInfo=new Label();ProgressBar progress=new ProgressBar();
 Button render,preview,openOutput,cancel,globalCancel; FlowLayoutPanel controls; Panel wavePanel=new Panel();
 List<Cue> cues=new List<Cue>();KokoroEngine active;string lastOutput,root,sourcePath;SoundPlayer player;Wave waveform;bool busy;TabControl tabs=new TabControl();
 VoiceCatalog catalog; List<BatchJob> jobs=new List<BatchJob>(); DataGridView jobGrid=new DataGridView(); TabPage batchPage; FlowLayoutPanel batchTools; Button runBatch; CheckBox skipFinished=new CheckBox(); ComboBox batchMode=new ComboBox(); Label batchInfo=new Label(); BatchJob editingJob;
 HashSet<string> favorites=new HashSet<string>();string stateDir;
 public MainForm() {
  root=AppDomain.CurrentDomain.BaseDirectory;stateDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"V15VoiceStudio");Directory.CreateDirectory(stateDir);
  if(File.Exists(Path.Combine(stateDir,"favorites.txt")))favorites=new HashSet<string>(File.ReadAllLines(Path.Combine(stateDir,"favorites.txt")));
  catalog=new VoiceCatalog(Path.Combine(root,"voice_profiles.tsv"),Path.Combine(stateDir,"voice_notes.tsv"));
  foreach(var own in UserVoices.Load(stateDir))catalog.Add(own);
  Text="V15 Voice Studio · Offline 1.4.4";MinimumSize=new Size(1100,740);Size=new Size(1280,850);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(244,247,251);
  var shell=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(20)};
  shell.RowStyles.Add(new RowStyle(SizeType.Absolute,82));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,52));Controls.Add(shell);
  var header=new Panel{Dock=DockStyle.Fill};header.Controls.Add(new Label{Text="V15 VOICE STUDIO",Font=new Font("Segoe UI",22,FontStyle.Bold),ForeColor=navy,AutoSize=true,Location=new Point(0,0)});
  header.Controls.Add(new Label{Text="OFFLINE  •  Danh sách giọng tuyển chọn • Lọc theo phong cách  •  SRT / TXT → WAV cạnh file gốc",AutoSize=true,ForeColor=muted,Location=new Point(3,45)});shell.Controls.Add(header,0,0);
  // Add pages in display order. Framework TabPages.Insert can lose pages
  // before the native TabControl handle exists (dotnet/winforms#3686).
  tabs.Dock=DockStyle.Fill;shell.Controls.Add(tabs,0,1);BuildStudio();BuildBatch();BuildLibrary();LoadEnhancements();BuildGuide();
  var foot=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220));
  status.Text="Sẵn sàng • Xử lý trên máy, không gửi kịch bản lên mạng";status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.ForeColor=muted;progress.Dock=DockStyle.Fill;progress.Margin=new Padding(0,16,0,12);globalCancel=Btn("Dừng tác vụ",(s,e)=>{if(active!=null)active.Cancel();});globalCancel.Enabled=false;globalCancel.Margin=new Padding(0,7,8,0);foot.Controls.Add(status,0,0);foot.Controls.Add(globalCancel,1,0);foot.Controls.Add(progress,2,0);shell.Controls.Add(foot,0,2);
  AllowDrop=true;DragEnter+=(s,e)=>{if(!busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};DragDrop+=(s,e)=>{var p=(string[])e.Data.GetData(DataFormats.FileDrop);if(p.Length>1)AddFiles(p);else if(p.Length==1)LoadFile(p[0]);};
  FormClosing+=(s,e)=>{if(busy){e.Cancel=true;if(active!=null)active.Cancel();status.Text="Đang dừng tác vụ. Đóng lại cửa sổ sau khi tác vụ dừng.";}else {SaveBreathSettings();if(player!=null)player.Dispose();}};
  Shown+=(s,e)=>{LoadSample();var session=StateFiles.Read(Path.Combine(stateDir,"session.xml"));if(session!=null)Apply(VoiceSettings.From(session));};
 }
 Button Btn(string text,EventHandler action,bool primary=false){var b=new Button{Text=text,AutoSize=true,Height=35,MinimumSize=new Size(90,35),FlatStyle=FlatStyle.Flat,BackColor=primary?teal:Color.White,ForeColor=primary?Color.White:navy,Margin=new Padding(0,0,8,6),Padding=new Padding(8,2,8,2)};b.FlatAppearance.BorderColor=Color.FromArgb(213,222,233);b.Click+=action;return b;}
 void BuildStudio(){
  var page=new TabPage("  Tạo voice  "){BackColor=Color.White,Padding=new Padding(14)};tabs.TabPages.Add(page);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,310));page.Controls.Add(layout);
  var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1};left.RowStyles.Add(new RowStyle(SizeType.Absolute,45));left.RowStyles.Add(new RowStyle(SizeType.Absolute,35));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,66));left.RowStyles.Add(new RowStyle(SizeType.Absolute,55));layout.Controls.Add(left,0,0);
  controls=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
  controls.Controls.Add(Btn("Mở SRT / TXT",(s,e)=>{using(var d=new OpenFileDialog{Filter="Kịch bản|*.srt;*.txt",Multiselect=true})if(d.ShowDialog()==DialogResult.OK){if(d.FileNames.Length>1)AddFiles(d.FileNames);else LoadFile(d.FileName);}}));
  controls.Controls.Add(Btn("Dán kịch bản",(s,e)=>Paste()));controls.Controls.Add(Btn("Mẫu V15",(s,e)=>LoadSample()));left.Controls.Add(controls,0,0);
  info.Text="Mở hoặc kéo thả SRT/TXT. Nhấp đôi ô để sửa lời và timecode.";info.ForeColor=muted;info.Dock=DockStyle.Fill;left.Controls.Add(info,0,1);
  grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.FixedSingle;grid.RowHeadersVisible=false;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowTemplate.MinimumHeight=44;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.EnableHeadersVisualStyles=false;
  grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=navy,ForeColor=Color.White,Padding=new Padding(4)};grid.ColumnHeadersHeight=34;grid.DefaultCellStyle.ForeColor=navy;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.DefaultCellStyle.Padding=new Padding(3);grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(247,249,252);
  grid.Columns.Add("id","#");grid.Columns.Add("start","Bắt đầu");grid.Columns.Add("end","Kết thúc");grid.Columns.Add("text","Lời đọc English");grid.Columns.Add("words","Từ");grid.Columns.Add("qa","V15 / căn nhịp");
  grid.Columns[0].Width=38;grid.Columns[1].Width=106;grid.Columns[2].Width=106;grid.Columns[3].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;grid.Columns[3].MinimumWidth=160;grid.Columns[4].Width=42;grid.Columns[5].Width=155;grid.Columns[0].ReadOnly=grid.Columns[4].ReadOnly=grid.Columns[5].ReadOnly=true;
  grid.CellEndEdit+=(s,e)=>{try{ReadGrid();RefreshSummary();}catch(Exception x){status.Text=x.Message;}};left.Controls.Add(grid,0,2);
  wavePanel.Dock=DockStyle.Fill;wavePanel.BackColor=Color.FromArgb(240,247,246);wavePanel.Margin=new Padding(0,10,0,0);wavePanel.Paint+=(s,e)=>DrawWave(e.Graphics);left.Controls.Add(wavePanel,0,3);
  var right=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(15,0,0,0)};layout.Controls.Add(right,1,0);
  AddLabel(right,"01  GIỌNG ĐỌC",true);family.DropDownStyle=ComboBoxStyle.DropDownList;family.Items.AddRange(new object[]{"Kokoro · "+catalog.All.Count(p=>p.Kind==EngineKind.Kokoro)+" giọng","LibriTTS · "+catalog.All.Count(p=>p.Kind==EngineKind.LibriTTS)+" giọng tuyển chọn","Kitten · "+catalog.All.Count(p=>p.Kind==EngineKind.Kitten)+" giọng English","Piper · "+catalog.All.Count(p=>p.Kind==EngineKind.Piper)+" giọng US","Qwen3-TTS · Aiden / Ryan / giọng riêng","Chatterbox · Nano / giọng riêng"});family.Width=250;family.SelectedIndexChanged+=(s,e)=>FillVoices();right.Controls.Add(family);
  styleFilter.DropDownStyle=ComboBoxStyle.DropDownList;styleFilter.Width=250;styleFilter.Items.AddRange(VoiceStyles.Names);styleFilter.SelectedIndex=0;styleFilter.SelectedIndexChanged+=(s,e)=>FillVoices();right.Controls.Add(styleFilter);
  voices.DropDownStyle=ComboBoxStyle.DropDownList;voices.Width=250;right.Controls.Add(voices);voices.SelectedIndexChanged+=(s,e)=>ShowVoiceInfo();family.SelectedIndex=0;
  voiceInfo.Width=250;voiceInfo.Height=145;voiceInfo.Font=new Font("Segoe UI",9);voiceInfo.ForeColor=muted;right.Controls.Add(voiceInfo);ShowVoiceInfo();
  preview=Btn("▶ Nghe thử câu đang chọn",async(s,e)=>await Preview());right.Controls.Add(preview);
  right.Controls.Add(Btn("▶ Nghe mẫu giọng có sẵn",(s,e)=>{var p=catalog.Get(Key());if(String.IsNullOrEmpty(p.Sample)){status.Text="Giọng này chưa có mẫu đóng gói. Dùng Nghe thử câu.";return;}Play(Path.Combine(root,p.Sample));}));
  right.Controls.Add(Btn("♡ Thêm / bỏ yêu thích",(s,e)=>ToggleFavorite()));
  AddLabel(right,"02  CĂN TIMELINE",true);mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Width=250;mode.Items.AddRange(new object[]{"Khóa SRT · giữ nguyên mốc","Linh hoạt · mốc theo voice"});mode.SelectedIndex=0;right.Controls.Add(mode);
  AddNumber(right,"Tốc độ đọc gốc (1.00 = bình thường)",speed,.8m,1.3m,1m,.05m);
  AddNumber(right,"Tốc độ tối đa khi căn SRT",maxSpeed,1m,1.6m,1.3m,.05m);
  AddNumber(right,"Khoảng nghỉ timeline linh hoạt (giây)",gap,.1m,.5m,.15m,.05m);
  slow.Text="Làm chậm nhẹ câu ngắn";slow.Checked=true;slow.AutoSize=true;right.Controls.Add(slow);
  BuildPacingControls(right);AddLabel(right,"04  HƠI THỞ",true);breathMode.DropDownStyle=ComboBoxStyle.DropDownList;breathMode.Width=250;breathMode.Items.AddRange(new object[]{"Auto","Tùy chọn","Không dùng"});right.Controls.Add(breathMode);
  breathSettings.FlowDirection=FlowDirection.TopDown;breathSettings.WrapContents=false;breathSettings.AutoSize=true;breathSettings.Width=250;breathSettings.Margin=new Padding(0);right.Controls.Add(breathSettings);
  breathPath.Width=245;breathPath.ReadOnly=true;breathSettings.Controls.Add(breathPath);
  breathSettings.Controls.Add(Btn("Chọn WAV hơi thở",(s,e)=>{using(var d=new OpenFileDialog{Filter="WAV PCM mono 16-bit|*.wav"})if(d.ShowDialog()==DialogResult.OK){try{var w=Wave.Read(d.FileName).TrimEdges();if(w.Duration>2)throw new Exception("Chọn mẫu hơi thở ngắn tối đa 2 giây.");breathPath.Text=d.FileName;}catch(Exception x){Error(x);}}}));
  breathSettings.Controls.Add(Btn("Dùng mẫu có sẵn",(s,e)=>breathPath.Text=""));
  AddNumber(breathSettings,"Âm lượng so với lời đọc (%)",breathVolume,1,60,16,1);breathVolume.DecimalPlaces=0;
  AddNumber(breathSettings,"Chèn sau mỗi số câu hoàn chỉnh",breathEvery,1,10,3,1);breathEvery.DecimalPlaces=0;
  breathMode.SelectedIndexChanged+=(s,e)=>{breathSettings.Visible=breathMode.SelectedIndex==1;};breathMode.SelectedIndex=0;LoadBreathSettings();
  AddLabel(right,"Auto dùng mẫu có sẵn, chèn thưa ở cuối câu khi đủ chỗ. Tùy chọn để chỉnh mẫu, âm lượng và tần suất.",false);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,8,0,0)};left.Controls.Add(actions,0,4);
  render=Btn("TẠO VOICE & XUẤT",async(s,e)=>await Export(),true);actions.Controls.Add(render);
  cancel=Btn("Dừng",(s,e)=>{if(active!=null)active.Cancel();});cancel.Enabled=false;actions.Controls.Add(cancel);
  var play=Btn("▶ Voice tổng",(s,e)=>{if(lastOutput!=null)Play(lastOutput);});actions.Controls.Add(play);
  right.Controls.Add(Btn("■ Dừng nghe",(s,e)=>{if(player!=null)player.Stop();}));
  openOutput=Btn("Mở kết quả",(s,e)=>{OpenResult(lastOutput);});openOutput.Enabled=false;actions.Controls.Add(openOutput);
 }
 void AddLabel(FlowLayoutPanel p,string text,bool bold){p.Controls.Add(new Label{Text=text,Width=250,AutoSize=true,MaximumSize=new Size(250,0),Margin=new Padding(0,bold?12:2,0,7),ForeColor=bold?navy:muted,Font=new Font("Segoe UI",bold?10:9,bold?FontStyle.Bold:FontStyle.Regular)});}
 void AddNumber(FlowLayoutPanel p,string text,NumericUpDown n,decimal min,decimal max,decimal val,decimal step){AddLabel(p,text,false);n.Minimum=min;n.Maximum=max;n.Value=val;n.DecimalPlaces=2;n.Increment=step;n.Width=100;p.Controls.Add(n);}
 void FillVoices(){
  string keep=Key(),style=styleFilter.SelectedIndex<=0?"":Convert.ToString(styleFilter.SelectedItem);voices.BeginUpdate();voices.Items.Clear();
  foreach(var p in catalog.All.Where(p=>(int)p.Kind==family.SelectedIndex&&p.MatchesStyle(style)))voices.Items.Add(new VoiceItem{Id=p.Speaker,Key=p.Key,Name=(p.Recommended?"★ ":"")+p.Name+" · "+p.Gender+" · "+p.MainStyle});
  voices.DropDownWidth=620;int selected=-1;for(int i=0;i<voices.Items.Count;i++)if(((VoiceItem)voices.Items[i]).Key==keep)selected=i;
  if(selected<0&&family.SelectedIndex==0){for(int i=0;i<voices.Items.Count;i++)if(((VoiceItem)voices.Items[i]).Key=="K:3")selected=i;}
  voices.SelectedIndex=voices.Items.Count==0?-1:Math.Max(0,selected);voices.EndUpdate();ShowVoiceInfo();
 }
 KokoroEngine NewEngine(){if(voices.SelectedItem==null)throw new Exception("Không có giọng phù hợp. Đổi nguồn hoặc chọn Tất cả phong cách.");return EngineFor(catalog.Get(Key()));}
 string Key(){var item=voices.SelectedItem as VoiceItem;return item==null?"":item.Key;}
 void ToggleFavorite(){string k=Key();if(String.IsNullOrEmpty(k))return;if(!favorites.Add(k))favorites.Remove(k);File.WriteAllLines(Path.Combine(stateDir,"favorites.txt"),favorites);status.Text=favorites.Contains(k)?"Đã lưu giọng yêu thích":"Đã bỏ giọng yêu thích";}
 void ShowVoiceInfo(){
  if(catalog==null)return;if(voices.SelectedItem==null){voiceInfo.Text="Không có giọng trong phong cách này. Chọn phong cách khác.";return;}
  var p=catalog.Get(Key());string blurb=p.Description.Length>110?p.Description.Substring(0,110)+"…":p.Description;
  voiceInfo.Text="Phù hợp: "+p.Styles.Replace("|",", ")+"\n"+blurb+(p.Recommended?"\n★ "+p.StarSource:"")+"\nXem lý do gợi ý tại Thư viện giọng.";
 }
 void BuildLibrary(){
  var page=new TabPage("  Thư viện & phong cách giọng  "){BackColor=Color.White,Padding=new Padding(16)};tabs.TabPages.Add(page);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,78));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,125));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,118));page.Controls.Add(layout);
  layout.Controls.Add(new Label{Text="Mỗi giọng có 1 phong cách chính và tối đa 2 phong cách phụ để dễ chọn. Phong cách là gợi ý sử dụng.\nLibriTTS chỉ hiển thị danh sách tuyển chọn; accent từng speaker chưa được chứng nhận.\n★ Kokoro theo nhà phát hành; ★ LibriTTS là giọng V15 ưu tiên thử qua kiểm tra mẫu, không phải điểm cộng đồng.",Dock=DockStyle.Fill,ForeColor=muted},0,0);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Fill};var search=new TextBox{Width=210};var style=new ComboBox{Width=240,DropDownStyle=ComboBoxStyle.DropDownList};style.Items.AddRange(VoiceStyles.Names);style.SelectedIndex=0;
  var fav=new CheckBox{Text="Chỉ giọng yêu thích",AutoSize=true};var high=new CheckBox{Text="★ Giọng ưu tiên",AutoSize=true};bar.Controls.Add(new Label{Text="Tìm giọng / đặc điểm:",AutoSize=true,Padding=new Padding(0,5,0,0)});bar.Controls.Add(search);bar.Controls.Add(style);bar.Controls.Add(fav);bar.Controls.Add(high);layout.Controls.Add(bar,0,1);
  var list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false};list.Columns.Add("Giọng",175);list.Columns.Add("Loại giọng",90);list.Columns.Add("Phong cách chính",180);list.Columns.Add("Phong cách phụ",260);list.Columns.Add("Đặc điểm",350);list.Columns.Add("Xếp hạng",100);list.Columns.Add("Yêu thích",80);layout.Controls.Add(list,0,2);
  var details=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(244,247,251)};layout.Controls.Add(details,0,3);
  var ownPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};ownPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));ownPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));ownPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,25));ownPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));ownPanel.Controls.Add(new Label{Text="Ghi chú riêng của bạn (được giữ khi cập nhật)",Dock=DockStyle.Fill},0,0);
  var own=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical};ownPanel.Controls.Add(own,0,1);layout.Controls.Add(ownPanel,0,4);
  Func<string> selected=()=>list.SelectedItems.Count==0?null:Convert.ToString(list.SelectedItems[0].Tag);
  Action fill=()=>{
   string keep=selected()??Key(),chosen=style.SelectedIndex<=0?"":Convert.ToString(style.SelectedItem);list.BeginUpdate();list.Items.Clear();
   foreach(var p in catalog.All){string user=catalog.Note(p.Key),all=p.Name+" "+p.Key+" "+p.Gender+" "+p.Description+" "+p.Styles+" "+user;
    if(!all.ToLowerInvariant().Contains(search.Text.ToLowerInvariant())||!p.MatchesStyle(chosen)||(fav.Checked&&!favorites.Contains(p.Key))||(high.Checked&&!p.Recommended))continue;
    var row=new ListViewItem(new[]{(p.Recommended?"★ ":"")+p.Name,p.Gender,p.MainStyle,String.Join(", ",p.Styles.Split('|').Skip(1)),p.Description+(user.Length>0?" | Bạn: "+user:""),p.Grade,favorites.Contains(p.Key)?"Đã lưu":""}){Tag=p.Key};if(p.Recommended)row.ForeColor=Color.DarkGoldenrod;list.Items.Add(row);if(p.Key==keep)row.Selected=true;
   }list.EndUpdate();if(list.SelectedItems.Count==0){details.Text="Chọn giọng để xem phong cách, lý do gợi ý và nguồn mô tả.";own.Text="";}
  };
  list.SelectedIndexChanged+=(s,e)=>{string key=selected();if(key==null)return;var p=catalog.Get(key);details.Text=p.Name+" ["+p.Key+"] • "+p.Gender+"\r\nPhù hợp: "+p.Styles.Replace("|",", ")+"\r\nLý do gợi ý: "+p.StyleReason+"\r\n"+p.Description+"\r\nNguồn: "+p.Source+(p.Recommended?"\r\n★ "+p.StarSource:"");own.Text=catalog.Note(key);};
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};ownPanel.Controls.Add(buttons,1,1);
  buttons.Controls.Add(Btn("Lưu ghi chú",(s,e)=>{string key=selected();if(key!=null){catalog.SaveNote(key,own.Text);fill();status.Text="Đã lưu ghi chú riêng.";}}));
  buttons.Controls.Add(Btn("Dùng giọng này",(s,e)=>{string key=selected();if(key==null||busy)return;var p=catalog.Get(key);styleFilter.SelectedIndex=0;family.SelectedIndex=(int)p.Kind;FillVoices();for(int i=0;i<voices.Items.Count;i++)if(((VoiceItem)voices.Items[i]).Key==key){voices.SelectedIndex=i;break;}tabs.SelectedIndex=0;}));
  list.DoubleClick+=(s,e)=>{if(!busy&&selected()!=null){var p=catalog.Get(selected());if(!String.IsNullOrEmpty(p.Sample))Play(Path.Combine(root,p.Sample));}};
  search.TextChanged+=(s,e)=>fill();style.SelectedIndexChanged+=(s,e)=>fill();fav.CheckedChanged+=(s,e)=>fill();high.CheckedChanged+=(s,e)=>fill();page.Enter+=(s,e)=>fill();fill();
 }
 void BuildBatch(){
  batchPage=new TabPage("  Hàng đợi nhiều file  "){BackColor=Color.White,Padding=new Padding(16)};tabs.TabPages.Add(batchPage);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,55));batchPage.Controls.Add(layout);
  layout.Controls.Add(new Label{Text="Chọn nhiều SRT/TXT hoặc kéo thả cả nhóm. App chạy lần lượt, dùng chung giọng, tốc độ và hơi thở ở tab Tạo voice.\nChỉ xuất WAV cùng tên ngay cạnh file gốc. File lỗi được ghi trên bảng; các file khác vẫn tiếp tục.",Dock=DockStyle.Fill,ForeColor=muted},0,0);
  batchTools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};batchTools.Controls.Add(Btn("Thêm nhiều file",(s,e)=>{using(var d=new OpenFileDialog{Filter="Kịch bản|*.srt;*.txt",Multiselect=true})if(d.ShowDialog()==DialogResult.OK)AddFiles(d.FileNames);}));
  batchTools.Controls.Add(Btn("Xem / sửa file",(s,e)=>EditQueued()));batchTools.Controls.Add(Btn("Bỏ file đã chọn",(s,e)=>{if(jobGrid.CurrentRow!=null){var j=jobs[jobGrid.CurrentRow.Index];if(editingJob==j)editingJob=null;jobs.Remove(j);RefreshJobs();}}));batchTools.Controls.Add(Btn("Xóa hàng đợi",(s,e)=>{jobs.Clear();editingJob=null;RefreshJobs();}));layout.Controls.Add(batchTools,0,1);
  batchInfo.Dock=DockStyle.Fill;batchInfo.ForeColor=muted;layout.Controls.Add(batchInfo,0,2);
  jobGrid.Dock=DockStyle.Fill;jobGrid.BackgroundColor=Color.White;jobGrid.RowHeadersVisible=false;jobGrid.AllowUserToAddRows=false;jobGrid.AllowUserToDeleteRows=false;jobGrid.ReadOnly=true;jobGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;jobGrid.MultiSelect=false;jobGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;jobGrid.RowTemplate.MinimumHeight=42;jobGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;jobGrid.DefaultCellStyle.ForeColor=navy;
  foreach(var title in new[]{"#","Tên file","Loại","Số câu","Trạng thái","Kết quả / lỗi"})jobGrid.Columns.Add(title,title);jobGrid.Columns[0].Width=40;jobGrid.Columns[1].Width=235;jobGrid.Columns[2].Width=105;jobGrid.Columns[3].Width=70;jobGrid.Columns[4].Width=140;jobGrid.Columns[5].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;jobGrid.CellDoubleClick+=(s,e)=>{if(!busy)EditQueued();};layout.Controls.Add(jobGrid,0,3);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};runBatch=Btn("CHẠY HÀNG ĐỢI",async(s,e)=>await ExportBatch(),true);actions.Controls.Add(runBatch);actions.Controls.Add(Btn("Dừng",(s,e)=>{if(active!=null)active.Cancel();}));
  batchMode.DropDownStyle=ComboBoxStyle.DropDownList;batchMode.Width=270;batchMode.Items.AddRange(new object[]{"Theo file · SRT khóa, TXT tự căn","Khóa timeline tất cả","Linh hoạt tất cả"});batchMode.SelectedIndex=0;actions.Controls.Add(batchMode);skipFinished.Text="Bỏ qua file đã xong";skipFinished.Checked=true;skipFinished.AutoSize=true;skipFinished.Padding=new Padding(0,8,0,0);actions.Controls.Add(skipFinished);actions.Controls.Add(Btn("Mở kết quả",(s,e)=>{OpenResult(jobGrid.CurrentRow==null?lastOutput:jobs[jobGrid.CurrentRow.Index].OutputPath);}));layout.Controls.Add(actions,0,4);RefreshJobs();
 }
 void AddFiles(IEnumerable<string> paths){if(busy)return;int added=0;foreach(string path in paths){if(jobs.Count>=500){status.Text="Mỗi hàng đợi tối đa 500 file.";break;}string full=Path.GetFullPath(path);if(jobs.Any(j=>String.Equals(j.InputPath,full,StringComparison.OrdinalIgnoreCase)))continue;jobs.Add(BatchJob.Load(full));added++;}RefreshJobs();tabs.SelectedTab=batchPage;status.Text="Đã thêm "+added+" file; lỗi nhập được ghi riêng trong bảng.";}
 void RefreshJobs(){if(jobGrid.Columns.Count==0)return;int index=jobGrid.CurrentRow==null?-1:jobGrid.CurrentRow.Index;jobGrid.Rows.Clear();for(int i=0;i<jobs.Count;i++){var j=jobs[i];string detail=j.Error.Length>0?j.Error:j.OutputPath;int row=jobGrid.Rows.Add(i+1,j.Name,j.Timed?"Có timeline":"TXT thường",j.Cues.Count,j.Status,detail);jobGrid.Rows[row].Cells[1].ToolTipText=j.InputPath;jobGrid.Rows[row].Cells[5].ToolTipText=detail;if(j.State==JobState.Failed||j.State==JobState.ImportFailed)jobGrid.Rows[row].DefaultCellStyle.BackColor=Color.MistyRose;else if(j.State==JobState.Done)jobGrid.Rows[row].DefaultCellStyle.BackColor=Color.FromArgb(226,245,235);}if(index>=0&&index<jobGrid.Rows.Count)jobGrid.CurrentCell=jobGrid.Rows[index].Cells[1];batchInfo.Text=jobs.Count+" file • "+jobs.Count(j=>j.State==JobState.Done)+" hoàn tất • "+jobs.Count(j=>j.State==JobState.Failed||j.State==JobState.ImportFailed)+" lỗi";SaveQueueIfChanged();}
 void EditQueued(){if(busy||jobGrid.CurrentRow==null)return;var j=jobs[jobGrid.CurrentRow.Index];if(j.State==JobState.ImportFailed){MessageBox.Show(j.Error,"File chưa nhập được");return;}editingJob=j;sourcePath=j.InputPath;cues=j.Cues;mode.SelectedIndex=j.Timed?0:1;Populate();tabs.SelectedIndex=0;status.Text="Đang sửa bản trong hàng đợi: "+j.Name+". File gốc được giữ nguyên.";}
 RenderOptions Options(){
  var o=new RenderOptions{Locked=mode.SelectedIndex==0,BaseSpeed=(double)speed.Value,MaxSpeed=(double)maxSpeed.Value,Gap=(double)gap.Value,SlowShort=slow.Checked,JoinSentences=joinSentences.Checked,Breathing=(BreathMode)breathMode.SelectedIndex,BreathPath=breathPath.Text,BreathLevel=(double)breathVolume.Value/100,BreathEvery=(int)breathEvery.Value};
  AddPacing(o);if(o.MaxSpeed<o.BaseSpeed)throw new Exception("Tốc độ tối đa phải >= tốc độ gốc.");new BreathTrack(o,root);SaveBreathSettings();SaveSession();return o;
 }
 void LoadBreathSettings(){try{string file=Path.Combine(stateDir,"breath_settings.txt");if(!File.Exists(file))return;var a=File.ReadAllLines(file);int m,e,v;if(a.Length==4&&int.TryParse(a[0],out m)&&m>=0&&m<=2&&int.TryParse(a[1],out v)&&v>=1&&v<=60&&int.TryParse(a[2],out e)&&e>=1&&e<=10){breathMode.SelectedIndex=m;breathVolume.Value=v;breathEvery.Value=e;breathPath.Text=Encoding.UTF8.GetString(Convert.FromBase64String(a[3]));}}catch{}}
 void SaveBreathSettings(){try{if(breathMode.SelectedIndex>=0)File.WriteAllLines(Path.Combine(stateDir,"breath_settings.txt"),new[]{breathMode.SelectedIndex.ToString(),breathVolume.Value.ToString("0",CultureInfo.InvariantCulture),breathEvery.Value.ToString("0",CultureInfo.InvariantCulture),Convert.ToBase64String(Encoding.UTF8.GetBytes(breathPath.Text))});}catch{}}
 void OpenResult(string file){try{if(String.IsNullOrEmpty(file)||!File.Exists(file)){status.Text="Chọn file đã hoàn tất để mở vị trí WAV.";return;}if(Environment.OSVersion.Platform==PlatformID.Win32NT)Process.Start(new ProcessStartInfo("explorer.exe","/select,"+KokoroEngine.Q(file)){UseShellExecute=true});else Process.Start(new ProcessStartInfo(Path.GetDirectoryName(file)){UseShellExecute=true});}catch(Exception e){Error(e);}}
 async Task ExportBatch(){
  try {
   if(jobs.Count==0)throw new Exception("Hãy thêm file vào hàng đợi.");if(editingJob!=null)ReadGrid();var o=Options();
   if(skipFinished.Checked&&jobs.All(j=>j.State==JobState.Done)){status.Text="Tất cả file đã hoàn tất. Bỏ chọn Bỏ qua file đã xong để chạy lại.";return;}
   active=NewEngine();active.CheckFiles();SetBusy(true);if(player!=null)player.Stop();int chosenMode=batchMode.SelectedIndex;bool skip=skipFinished.Checked;var engine=active;
   lastOutput=await Task.Run(()=>BatchRunner.Run(jobs,skip,()=>engine.Cancelled,(j,file,notify)=>{o.Locked=chosenMode==1||(chosenMode==0&&j.Timed);j.Cues=RenderCues(j.Cues,o);return Renderer.Render(j.Cues,engine,o,file,notify);},(p,msg)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{progress.Value=Math.Min(100,Math.Max(0,p));status.Text=msg;RefreshJobs();}));}));
   openOutput.Enabled=!String.IsNullOrEmpty(lastOutput);int done=jobs.Count(j=>j.State==JobState.Done),errors=jobs.Count(j=>j.State==JobState.Failed||j.State==JobState.ImportFailed);status.Text=(engine.Cancelled?"Đã dừng":"Xong hàng đợi")+" • "+done+" hoàn tất • "+errors+" lỗi. WAV nằm cạnh từng SRT/TXT gốc.";
  }catch(Exception e){Error(e);}finally{if(active!=null)active.Dispose();SetBusy(false);active=null;if(editingJob!=null){cues=editingJob.Cues;Populate();}RefreshJobs();SaveSession();}
 }
 void BuildGuide(){var page=new TabPage("  Hướng dẫn  "){BackColor=Color.White,Padding=new Padding(20)};tabs.TabPages.Add(page);var box=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=Color.White,Font=new Font("Segoe UI",11)};box.Text=File.Exists(Path.Combine(root,"HUONG_DAN_1.4.4.txt"))?File.ReadAllText(Path.Combine(root,"HUONG_DAN_1.4.4.txt")):File.Exists(Path.Combine(root,"HUONG_DAN.txt"))?File.ReadAllText(Path.Combine(root,"HUONG_DAN.txt")):"Mở SRT/TXT → chọn giọng → nghe thử → tạo voice. Chỉ xuất WAV cạnh file SRT/TXT gốc.\r\nHoàn toàn offline. Hơi thở: Auto, Tùy chọn hoặc Không dùng.";page.Controls.Add(box);}
 void LoadFile(string path){try{if(busy)return;var job=BatchJob.Load(path);if(job.State==JobState.ImportFailed)throw new Exception(job.Error);editingJob=null;cues=job.Cues;mode.SelectedIndex=job.Timed?0:1;sourcePath=job.InputPath;Populate();status.Text="Đã mở "+Path.GetFileName(path);}catch(Exception e){if(!busy){cues=new List<Cue>();sourcePath=null;editingJob=null;waveform=null;lastOutput=null;openOutput.Enabled=false;Populate();info.Text="Chưa nhập được "+Path.GetFileName(path)+" · "+e.Message;}Error(e);}}
 void LoadSample(){try{editingJob=null;sourcePath=null;cues=Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:03,300\nHave you ever forgotten why you entered a room?\n\n2\n00:00:03,450 --> 00:00:06,750\nYour brain may have switched contexts at the doorway.\n\n3\n00:00:06,900 --> 00:00:10,200\nBut the unfinished thought can still come back later.\n");mode.SelectedIndex=0;Populate();}catch(Exception e){Error(e);}}
 void Paste(){using(var f=new Form{Text="Dán SRT hoặc TXT tiếng Anh",Size=new Size(780,520),StartPosition=FormStartPosition.CenterParent}){var t=new TextBox{Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Font=new Font("Consolas",11)};var b=new Button{Text="Nhập kịch bản",Dock=DockStyle.Bottom,Height=42};f.Controls.Add(t);f.Controls.Add(b);b.Click+=(s,e)=>{try{editingJob=null;cues=t.Text.Contains("-->")?Scripts.ParseSrt(t.Text):Scripts.ParseText(t.Text);mode.SelectedIndex=(t.Text.Contains("-->")||System.Text.RegularExpressions.Regex.IsMatch(t.Text,@"\d+:\d{2}:\d{2}"))?0:1;sourcePath=null;Populate();f.Close();}catch(Exception x){Error(x);}};f.ShowDialog(this);}}
 void Populate(){grid.Rows.Clear();foreach(var c in cues){int i=grid.Rows.Add(c.Id,Scripts.Stamp(c.Start),Scripts.Stamp(c.End),c.Text,c.Words,c.Status=="Chưa tạo"?Scripts.Advice(c):c.Status);if(c.Status.StartsWith("QUÁ"))grid.Rows[i].DefaultCellStyle.BackColor=Color.MistyRose;else if(c.Words>10)grid.Rows[i].DefaultCellStyle.BackColor=Color.FromArgb(255,248,225);}RefreshSummary();}
 void ReadGrid(){grid.EndEdit();bool changed=false;var updated=new List<Cue>();for(int i=0;i<grid.Rows.Count;i++){var r=grid.Rows[i];var old=cues[i];updated.Add(new Cue{Id=i+1,Start=Scripts.Time(Convert.ToString(r.Cells[1].Value)),End=Scripts.Time(Convert.ToString(r.Cells[2].Value)),Text=Convert.ToString(r.Cells[3].Value),Natural=old.Natural,Actual=old.Actual,Speed=old.Speed,AudioPath=old.AudioPath,Status=old.Status});var current=updated.Last();if(current.Text!=old.Text||current.Start!=old.Start||current.End!=old.End){changed=true;current.Natural=current.Actual=0;current.Status="Chưa tạo";current.AudioPath=null;}}Scripts.Validate(updated);cues=updated;if(editingJob!=null){editingJob.Cues=updated;if(changed){editingJob.State=JobState.Queued;editingJob.Error="";}RefreshJobs();}if(changed)SaveSession();}
 void RefreshSummary(){if(cues.Count==0){info.Text="Chưa có kịch bản được nhập thành công.";return;}info.Text=cues.Count+" câu  ·  "+cues.Sum(c=>c.Words)+" từ  ·  "+Scripts.Stamp(cues.Last().End)+"  ·  "+cues.Count(c=>c.Words>10)+" câu >10 từ";for(int i=0;i<cues.Count&&i<grid.Rows.Count;i++){grid.Rows[i].Cells[4].Value=cues[i].Words;grid.Rows[i].Cells[5].Value=cues[i].Status=="Chưa tạo"?Scripts.Advice(cues[i]):cues[i].Status;}}
 void SetBusy(bool value){busy=value;if(globalCancel!=null)globalCancel.Enabled=value;foreach(var control in extraControls)control.Enabled=!value;if(batchTools!=null)batchTools.Enabled=!value;if(runBatch!=null)runBatch.Enabled=!value;batchMode.Enabled=skipFinished.Enabled=jobGrid.Enabled=!value;grid.ReadOnly=value;controls.Enabled=!value;family.Enabled=voices.Enabled=styleFilter.Enabled=mode.Enabled=speed.Enabled=maxSpeed.Enabled=gap.Enabled=slow.Enabled=breathMode.Enabled=breathSettings.Enabled=render.Enabled=preview.Enabled=!value;cancel.Enabled=value;UseWaitCursor=value;}
 async Task Preview(){try{ReadGrid();if(cues.Count==0)return;int i=grid.CurrentRow==null?0:grid.CurrentRow.Index;active=NewEngine();active.CheckFiles();SetBusy(true);status.Text="Đang tạo mẫu giọng offline…";string path=Path.Combine(stateDir,"preview_"+Guid.NewGuid().ToString("N")+".wav");string text=cues[i].Text;double sp=(double)speed.Value;var pacing=PacingOptions();var w=await Task.Run(()=>Punctuation.Generate(active,text,sp,pacing,path).Join());w.Write(path);waveform=w;wavePanel.Invalidate();Play(path);status.Text="Mẫu "+w.Duration.ToString("0.00")+"s • khung "+cues[i].Duration.ToString("0.00")+"s";}catch(OperationCanceledException){status.Text="Đã dừng nghe thử";}catch(Exception e){Error(e);}finally{if(active!=null)active.Dispose();SetBusy(false);active=null;}}
 async Task Export(){try{
  ReadGrid();var o=Options();string output;
  if(!String.IsNullOrEmpty(sourcePath))output=OutputFiles.Beside(sourcePath);
  else {using(var d=new SaveFileDialog{Filter="WAV|*.wav",FileName="kich_ban.wav",Title="Chọn nơi lưu WAV cho kịch bản dán / mẫu",OverwritePrompt=false}){if(d.ShowDialog()!=DialogResult.OK)return;output=d.FileName;}}
  cues=RenderCues(cues,o);if(editingJob!=null)editingJob.Cues=cues;Populate();active=NewEngine();active.CheckFiles();SetBusy(true);if(player!=null)player.Stop();progress.Value=0;var engine=active;
  lastOutput=await Task.Run(()=>Renderer.Render(cues,engine,o,output,(p,msg)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{progress.Value=p;status.Text=msg;}));}));
  waveform=new FileInfo(lastOutput).Length<=32*1024*1024?Wave.Read(lastOutput):null;wavePanel.Invalidate();openOutput.Enabled=true;
  if(editingJob!=null){editingJob.State=JobState.Done;editingJob.Error="";editingJob.OutputPath=lastOutput;RefreshJobs();}
  status.Text="Hoàn tất • "+lastOutput;
 }catch(OperationCanceledException){status.Text="Đã dừng • chưa xuất WAV";}catch(Exception e){Error(e);}finally{if(active!=null)active.Dispose();SetBusy(false);active=null;Populate();SaveSession();}}
 void Play(string path){try{if(!File.Exists(path))throw new Exception("Chưa có file voice tổng.");if(new FileInfo(path).Length>32*1024*1024){Process.Start(new ProcessStartInfo(path){UseShellExecute=true});return;}if(player!=null)player.Dispose();player=new SoundPlayer(path);player.Play();}catch(Exception e){Error(e);}}
 void DrawWave(Graphics g){int width=wavePanel.ClientSize.Width,height=wavePanel.ClientSize.Height;using(var pen=new Pen(teal)){g.DrawLine(Pens.LightGray,0,height/2,width,height/2);if(waveform==null){g.DrawString("Waveform sẽ hiện sau khi nghe thử hoặc xuất voice",Font,Brushes.SlateGray,10,15);return;}for(int x=0;x<width;x++){int a=(int)((long)x*waveform.Samples.Length/Math.Max(1,width)),b=(int)((long)(x+1)*waveform.Samples.Length/Math.Max(1,width)),peak=0;for(int j=a;j<b;j+=Math.Max(1,(b-a)/30))peak=Math.Max(peak,Math.Abs((int)waveform.Samples[j]));float v=peak/32768f*(height/2-3);g.DrawLine(pen,x,height/2-v,x,height/2+v);}}}
 void Error(Exception e){status.Text=e.Message.Split(new char[] { '\n' }, StringSplitOptions.None)[0];MessageBox.Show(this,e.Message,"V15 Voice Studio",MessageBoxButtons.OK,MessageBoxIcon.Information);}
}
public static class Program {
 [STAThread]public static void Main(string[] args){string folder=AppDomain.CurrentDomain.BaseDirectory,updateLock=Path.Combine(folder,".v15-update.lock");if(File.Exists(updateLock)){try{using(var check=File.Open(updateLock,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){} }catch(IOException){MessageBox.Show("Ứng dụng đang được cập nhật. Vui lòng chờ công cụ cập nhật hoàn tất.","V15");return;}}if(File.Exists(Path.Combine(folder,".v15-update-journal.xml"))){MessageBox.Show("Có cập nhật chưa hoàn tất. Chạy lại CAP_NHAT.cmd và chọn thư mục này để khôi phục.","V15");return;}bool created;using(var mutex=new System.Threading.Mutex(true,@"Local\V15VoiceStudio",out created)){if(!created){MessageBox.Show("V15 Voice Studio đang mở. Dùng hàng đợi để chạy nhiều file trong cùng ứng dụng.","V15 Voice Studio");return;}try{Run(args);}finally{mutex.ReleaseMutex();}}}
 public static void Run(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Contains("--smoke-ui")){var f=new MainForm();typeof(MainForm).GetMethod("LoadSample",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);var timer=new Timer{Interval=1500};timer.Tick+=(s,e)=>{timer.Stop();var table=(DataGridView)typeof(MainForm).GetField("grid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);Console.WriteLine("GRID="+table.Rows.Count+":"+String.Join("|",table.Rows[0].Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value))));using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(0,0,f.Width,f.Height));bitmap.Save(Path.Combine(Path.GetTempPath(),"v15-ui.png"));}f.Close();};timer.Start();Application.Run(f);return;}Application.Run(new MainForm());}
}
}
