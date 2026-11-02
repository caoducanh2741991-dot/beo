using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;
namespace V15Voice {
public static class CommunityVoices {
 public const string AdamFolder="rvc_adam";
 public static string Home(string root){return Path.Combine(root,"source","community");}
 public static string RvcHome(string root){return Path.Combine(Home(root),"rvc");}
 public static void Add(VoiceCatalog catalog){
  catalog.Add(new VoiceProfile{Key="C:adam_rvc",Name="Adam · RVC cộng đồng",Kind=EngineKind.Rvc,ModelFolder=AdamFolder,Speaker=0,Gender="Nam",Styles="Kể chuyện|Tài liệu / Thuyết minh|Review",Description="Model cộng đồng mang tên Adam Elevenlabs. Chuyển giọng từ Kokoro Michael sang Adam bằng RVC; nhịp và cách diễn đạt phụ thuộc giọng nền. Nghe thử để đánh giá.",Source="Coolwowsocoolwow/Adam_Elevenlabs · nhãn OpenRAIL của người đăng · https://huggingface.co/Coolwowsocoolwow/Adam_Elevenlabs",StyleReason="Gợi ý thử cho kể chuyện/thuyết minh; chưa có điểm đánh giá độ giống độc lập. Không phải ElevenLabs Adam chính thức, không phải Kokoro Adam. Dùng index gốc 0.75 và bảo vệ phụ âm 0.33. Chưa xác minh quyền khai thác thương mại từ nguồn gốc dữ liệu.",Grade="Chưa chấm sao",Recommended=false,Sample="source/community/rvc/sample.wav"});
 }
 public static string RvcModel(string root){return Path.Combine(RvcHome(root),"weights","adam.pth");}
 public static string Fingerprint(KokoroEngine e){if(e.Kind!=EngineKind.Rvc)return "";var files=new[]{RvcModel(e.Root),Path.Combine(RvcHome(e.Root),"weights","adam.index"),Path.Combine(RvcHome(e.Root),"weights","hubert.bin"),Path.Combine(RvcHome(e.Root),"weights","rmvpe.pt"),Path.Combine(Home(e.Root),"rvc_worker.py"),Path.Combine(e.Root,"model","model.onnx"),Path.Combine(e.Root,"model","voices.bin")};return "RVC-2.0.10-Michael16-index075-protect033|"+String.Join("|",files.Select(p=>{var f=new FileInfo(p);return f.Exists?f.Length+":"+f.LastWriteTimeUtc.Ticks:"missing";}));}
 public static void CheckRvc(KokoroEngine e){
  if(e.ModelFolder!=AdamFolder)throw new Exception("Bộ RVC chưa được hỗ trợ.");
  var files=new List<string>{Path.Combine(Home(e.Root),"rvc_worker.py"),RvcModel(e.Root),Path.Combine(RvcHome(e.Root),"weights","adam.index"),Path.Combine(RvcHome(e.Root),"weights","hubert.bin"),Path.Combine(RvcHome(e.Root),"weights","hubert-config.json"),Path.Combine(RvcHome(e.Root),"weights","rmvpe.pt")};
  if(Environment.OSVersion.Platform==PlatformID.Win32NT)files.Add(ExternalSpeech.Python(e.Root));
  foreach(string path in files)if(!File.Exists(path))throw new Exception("Thiếu gói Adam RVC: "+path+". Cài đủ ZIP 2.0.10 có dữ liệu giọng cộng đồng.");
  using(var b=BaseEngine(e))b.CheckFiles();
 }
 public static KokoroEngine BaseEngine(KokoroEngine e){return new KokoroEngine{Root=e.Root,Kind=EngineKind.Kokoro,Speaker=16,VoiceName="Michael · nền cho Adam RVC",DeviceMode=e.DeviceMode,Threads=e.Threads,ActivityChanged=e.ActivityChanged,DeviceChanged=s=>e.ReportActivity("Tạo lời nền · "+s)};}
}
public sealed class RvcSpeech:IDisposable {
 readonly KokoroEngine owner,baseEngine;readonly ExternalSpeech converter;
 public RvcSpeech(KokoroEngine engine){owner=engine;baseEngine=CommunityVoices.BaseEngine(engine);converter=new ExternalSpeech(engine);}
 public Wave Generate(string text,double speed,string output){
  string input=output+".rvc-"+Guid.NewGuid().ToString("N")+".wav";
  try{if(owner.Cancelled)throw new OperationCanceledException();owner.ReportActivity("Adam RVC · 1/2 tạo lời nền Michael");baseEngine.RunReport=owner.RunReport;baseEngine.RenderContext=owner.RenderContext;var w=baseEngine.Generate(text,speed,input);w.Write(input);if(owner.Cancelled)throw new OperationCanceledException();owner.ReportActivity("Adam RVC · 2/2 chuyển giọng, sau đó đo và căn timeline");var result=converter.Generate(input,1,output);owner.ReportDevice("Adam RVC · "+owner.DeviceInfo+" | lời nền Michael (Kokoro 16): "+baseEngine.DeviceInfo);return result;}
  finally{try{File.Delete(input);}catch{}}
 }
 public void Cancel(){baseEngine.Cancel();converter.Cancel();}
 public void Dispose(){converter.Dispose();baseEngine.Dispose();}
}
public partial class MainForm {
 void ShowCommunityVoices(){if(busy)return;
  using(var f=new Form{Text="Giọng cộng đồng · Sarah & Adam",Size=new Size(850,640),MinimumSize=new Size(730,520),StartPosition=FormStartPosition.CenterParent,Font=Font}){
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));f.Controls.Add(layout);
   layout.Controls.Add(new Label{Text="Adam RVC · dùng offline · chưa chấm sao",Dock=DockStyle.Fill,Font=new Font(Font,FontStyle.Bold)},0,0);
   var report=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};layout.Controls.Add(report,0,1);
   var messages=new List<string>();foreach(string key in new[]{"C:adam_rvc"}){var p=catalog.Get(key);string check;using(var e=EngineFor(p))try{e.CheckFiles();check="Đủ tệp model / bộ đọc. Nghe thử để kiểm tra chạy thực tế.";}catch(Exception x){check="Chưa sẵn sàng: "+x.Message;}messages.Add(p.Name+"\r\n"+check+"\r\nPhù hợp: "+p.Styles.Replace("|",", ")+"\r\n"+p.Description+"\r\n"+p.Source+"\r\n"+p.StyleReason);}
   report.Text="SARAH ATLAS · CHƯA ĐƯA VÀO DANH SÁCH\r\nModel nguồn sinh WAV nhưng các mẫu kiểm tra không ra lời đọc rõ bằng cả Piper gốc và sherpa-onnx. Đã loại khỏi gói sử dụng; xem ghi chú đi kèm bản cập nhật.\r\n\r\n"+String.Join("\r\n\r\n",messages)+"\r\n\r\nAuto: dùng GPU phù hợp và dự phòng CPU khi cần. Bắt buộc GPU: Adam cần cả gói ONNX (lời nền) lẫn CUDA124 (RVC). Model RVC được giữ trong tác vụ để các câu tiếp theo không phải nạp lại. Kết quả vẫn là WAV và báo cáo cạnh SRT/TXT; dùng được trong hàng đợi và A/B.";
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};layout.Controls.Add(buttons,0,2);foreach(string key in new[]{"C:adam_rvc"}){string selected=key;buttons.Controls.Add(Btn("Dùng Adam RVC",(s,e)=>{SelectVoice(selected);tabs.SelectedIndex=0;f.Close();}));}buttons.Controls.Add(Btn("Đóng",(s,e)=>f.Close()));f.ShowDialog(this);
  }
 }
}
}
