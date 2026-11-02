using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.5", FrameworkDisplayName=".NET Framework 4.5")]
[assembly: System.Reflection.AssemblyVersion("1.4.6.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.4.6.0")]

namespace V15Update {
public static class UpdateEngine {
 public static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 public static string Safe(string root,string relative){
  if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.IndexOf('\\')>=0||relative.IndexOf(':')>=0)throw new Exception("Đường dẫn gói cập nhật không hợp lệ.");
  var reserved=new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"};
  foreach(string part in relative.Split('/'))if(part==""||part=="."||part==".."||part.EndsWith(".")||part.EndsWith(" ")||part.IndexOfAny(new[]{'<','>','"','|','?','*','\0'})>=0||reserved.Contains(part.Split('.')[0].ToUpperInvariant()))throw new Exception("Tên tệp cập nhật không an toàn.");
  string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar))),prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("Đường dẫn thoát khỏi thư mục ứng dụng.");
  string current=Path.GetDirectoryName(full);while(current!=null&&current.Length>=Path.GetFullPath(root).Length){if(Directory.Exists(current)&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new Exception("Không cập nhật qua liên kết thư mục.");current=Path.GetDirectoryName(current);}
  if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new Exception("Không thay thế liên kết tệp.");return full;
 }
 static XElement Xml(Stream stream){var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024};using(var reader=XmlReader.Create(stream,settings))return XElement.Load(reader);}
 static XElement Manifest(string package){if(Directory.Exists(package)){using(var s=File.OpenRead(Path.Combine(package,"manifest.xml")))return Xml(s);}using(var z=ZipFile.OpenRead(package)){var e=z.GetEntry("manifest.xml");if(e==null)throw new Exception("ZIP không có manifest cập nhật V15.");using(var s=e.Open())return Xml(s);}}
 static string A(XElement x,string name){return (string)x.Attribute(name)??"";}
 static long Size(XElement x){long size;if(!Int64.TryParse(A(x,"size"),out size)||size<0||size>12L*1024*1024*1024)throw new Exception("Kích thước cập nhật không hợp lệ.");return size;}
 static void CopyChecked(Stream input,string output,long length,string hash){
  if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new Exception("Thiếu SHA-256 hợp lệ.");Directory.CreateDirectory(Path.GetDirectoryName(output));long total=0;var buffer=new byte[1024*1024];using(var target=File.Create(output)){int n;while((n=input.Read(buffer,0,buffer.Length))>0){total+=n;if(total>length)throw new Exception("Dữ liệu dài hơn khai báo.");target.Write(buffer,0,n);}}
  if(total!=length||!Hash(output).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new Exception("Dữ liệu tải về bị thiếu hoặc sai mã kiểm tra: "+Path.GetFileName(output));
 }
 static bool AllowedCore(string path){return path=="V15VoiceStudio.exe"||path=="V15Updater.exe"||path=="ai/worker.py"||path=="ai/gpu_policy.py"||(path.StartsWith("HUONG_DAN_")&&path.EndsWith(".txt"))||path.StartsWith("source/");}
 public static List<string> Stage(string package,string stage,Action<string> notify){
  var manifest=Manifest(package);Version release;if(manifest.Name!="update"||A(manifest,"product")!="V15VoiceStudio"||!Version.TryParse(A(manifest,"version"),out release)||release<new Version(1,4,1))throw new Exception("Gói này không phải cập nhật V15 được hỗ trợ.");
  var result=new List<string>();string kind=A(manifest,"kind");Directory.CreateDirectory(stage);
  if(kind=="core"){
   var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in manifest.Elements("file")){string path=A(f,"path");if(!AllowedCore(path)||!seen.Add(path))throw new Exception("Tệp không được phép hoặc trùng tên: "+path);string dest=Safe(stage,path),entry="payload/"+path;
    if(Directory.Exists(package)){using(var s=File.OpenRead(Safe(package,entry)))CopyChecked(s,dest,Size(f),A(f,"sha256"));}else using(var zip=ZipFile.OpenRead(package)){var e=zip.GetEntry(entry);if(e==null)throw new Exception("ZIP thiếu "+entry);using(var s=e.Open())CopyChecked(s,dest,Size(f),A(f,"sha256"));}result.Add(path);
   }if(!result.Contains("V15VoiceStudio.exe")||!result.Contains("V15Updater.exe"))throw new Exception("Gói cập nhật thiếu ứng dụng.");
  }else if(kind=="onnx"){
   string folder=Directory.Exists(package)?package:Path.GetDirectoryName(Path.GetFullPath(package));var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var rows=manifest.Elements("file").ToList();
   if(rows.Count<4||rows.Count>128)throw new Exception("Gói ONNX không đủ thư viện hoặc có quá nhiều tệp.");
   foreach(var f in rows){string path=A(f,"path");string name=path.StartsWith("engine/gpu/")?path.Substring(11):"";
    if(name==""||name.IndexOf('/')>=0||!(name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||name=="sherpa-onnx-offline-tts.exe"||name=="info.json"||name.StartsWith("LICENSE-"))||!seen.Add(path))throw new Exception("Tệp GPU ONNX không hợp lệ hoặc trùng: "+path);Safe(stage,path);
    var pieces=f.Elements("piece").ToList();if(pieces.Count==0||pieces.Count>256)throw new Exception("Tệp ONNX thiếu dữ liệu.");
    foreach(var p in pieces){string arc=A(p,"archive");if(Path.GetFileName(arc)!=arc||!arc.StartsWith("V15_ONNX_GPU_1.4.3_")||!arc.EndsWith(".zip"))throw new Exception("Tên phần ONNX không hợp lệ.");if(!File.Exists(Safe(folder,arc)))throw new Exception("Thiếu "+arc+". Đặt đủ 4 ZIP ONNX trong cùng thư mục.");}
   }
   foreach(string name in new[]{"sherpa-onnx-offline-tts.exe","onnxruntime.dll","onnxruntime_providers_cuda.dll","onnxruntime_providers_shared.dll"})if(!seen.Contains("engine/gpu/"+name))throw new Exception("Thiếu thư viện ONNX bắt buộc: "+name);
   int index=0;foreach(var f in rows){string dest=Safe(stage,A(f,"path"));Directory.CreateDirectory(Path.GetDirectoryName(dest));notify("Kiểm tra và ghép GPU ONNX "+(++index)+" / "+rows.Count+": "+Path.GetFileName(dest));long total=0;
    using(var output=File.Create(dest))foreach(var p in f.Elements("piece")){string temp=Safe(stage,".onnx-piece.tmp");using(var z=ZipFile.OpenRead(Safe(folder,A(p,"archive")))){var entry=z.GetEntry(A(p,"entry"));if(entry==null)throw new Exception("Phần ONNX thiếu dữ liệu: "+A(p,"archive"));try{using(var input=entry.Open())CopyChecked(input,temp,Size(p),A(p,"sha256"));}catch(Exception e){throw new Exception("Hỏng "+A(p,"archive")+": "+e.Message,e);}using(var input=File.OpenRead(temp))input.CopyTo(output);File.Delete(temp);total+=Size(p);if(total>Size(f))throw new Exception("Tệp ONNX dài hơn khai báo.");}}
    if(total!=Size(f)||!Hash(dest).Equals(A(f,"sha256"),StringComparison.OrdinalIgnoreCase))throw new Exception("Sai SHA-256 thư viện ONNX: "+Path.GetFileName(dest));result.Add(A(f,"path"));
   }
  }else if(kind=="gpu"){
   string folder=Directory.Exists(package)?package:Path.GetDirectoryName(Path.GetFullPath(package));var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var wheels=manifest.Elements("wheel").ToList();if(wheels.Count!=2)throw new Exception("Gói GPU cần đủ torch và torchaudio.");
   foreach(var piece in wheels.SelectMany(w=>w.Elements("piece"))){string arc=A(piece,"archive");if(Path.GetFileName(arc)!=arc||!File.Exists(Safe(folder,arc)))throw new Exception("Thiếu "+arc+". Đặt đủ các ZIP GPU vào cùng thư mục.");}
   foreach(var wheel in wheels){string name=A(wheel,"name");if(name!="torch.whl"&&name!="torchaudio.whl")throw new Exception("Wheel không hợp lệ.");string wheelFile=Safe(stage,".wheels/"+name);Directory.CreateDirectory(Path.GetDirectoryName(wheelFile));
    notify("Đang kiểm tra và ghép thư viện GPU: "+name);long total=0;
    using(var output=File.Create(wheelFile))foreach(var piece in wheel.Elements("piece")){
     string archive=A(piece,"archive");if(Path.GetFileName(archive)!=archive||!archive.StartsWith("V15_GPU_CUDA124_")||!archive.EndsWith(".zip"))throw new Exception("Tên phần GPU không hợp lệ.");string archiveFile=Safe(folder,archive);if(!File.Exists(archiveFile))throw new Exception("Thiếu "+archive+". Đặt đủ các ZIP GPU vào cùng thư mục.");
     using(var z=ZipFile.OpenRead(archiveFile)){var e=z.GetEntry(A(piece,"entry"));if(e==null)throw new Exception("Phần GPU thiếu dữ liệu.");string temp=Safe(stage,".wheels/piece.tmp");try{using(var input=e.Open())CopyChecked(input,temp,Size(piece),A(piece,"sha256"));}catch(Exception error){throw new Exception("Hỏng "+archive+": "+error.Message,error);}using(var input=File.OpenRead(temp))input.CopyTo(output);File.Delete(temp);total+=Size(piece);}
    }
    if(total!=Size(wheel)||Hash(wheelFile)!=A(wheel,"sha256"))throw new Exception("Thư viện GPU không khớp SHA-256.");
    notify("Giải nén thư viện CUDA: "+name);using(var z=ZipFile.OpenRead(wheelFile))foreach(var e in z.Entries){if(e.FullName.EndsWith("/"))continue;string path=e.FullName;
     if(!(path.StartsWith("torch/")||path.StartsWith("torchaudio/")||path.StartsWith("functorch/")||path.StartsWith("torchgen/")||path.StartsWith("torio/")||path.StartsWith("torch-2.6.0+")||path.StartsWith("torchaudio-2.6.0+")))throw new Exception("Nội dung wheel ngoài phạm vi: "+path);
     string relative="ai/gpu/site-packages/"+path;if(!seen.Add(relative))throw new Exception("Wheel trùng đường dẫn.");string target=Safe(stage,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));using(var input=e.Open())using(var output=File.Create(target)){var buffer=new byte[1024*1024];long bytes=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){bytes+=n;if(bytes>e.Length||bytes>6L*1024*1024*1024)throw new Exception("Wheel bị hỏng.");output.Write(buffer,0,n);}if(bytes!=e.Length)throw new Exception("Wheel thiếu dữ liệu.");}result.Add(relative);
    }File.Delete(wheelFile);
   }
  }else throw new Exception("Loại cập nhật không hỗ trợ.");return result;
 }
 static void SaveJournal(string path,XElement journal){string tmp=path+".tmp";journal.Save(tmp);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
 public static void Recover(string target){string path=Path.Combine(target,".v15-update-journal.xml");if(!File.Exists(path))return;XElement journal;using(var s=File.OpenRead(path))journal=Xml(s);string backup=A(journal,"backup");if(!backup.StartsWith(".v15-backup-")||backup.IndexOf('/')>=0||backup.IndexOf('\\')>=0)throw new Exception("Nhật ký khôi phục không hợp lệ.");string backupDir=Safe(target,backup);
  foreach(var f in journal.Elements("file").Reverse()){
   string dest=Safe(target,A(f,"path")),old=Safe(backupDir,A(f,"path"));if(File.Exists(old)){if(File.Exists(dest))File.Delete(dest);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Move(old,dest);}else if(A(f,"old")=="0"&&A(f,"ready")=="1"&&File.Exists(dest)&&Hash(dest)==A(f,"newHash"))File.Delete(dest);
  }File.Delete(path);if(Directory.Exists(backupDir))Directory.Delete(backupDir,true);
 }
 public static void Commit(string stage,string target,List<string> files,Action<string> notify,Action<int> fault=null){
  string journalPath=Path.Combine(target,".v15-update-journal.xml"),backupName=".v15-backup-"+Guid.NewGuid().ToString("N"),backup=Safe(target,backupName);Directory.CreateDirectory(backup);File.WriteAllText(Path.Combine(backup,".v15-owner"),"V15VoiceStudio");var journal=new XElement("transaction",new XAttribute("backup",backupName));
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string f in files){string dest=Safe(target,f),source=Safe(stage,f);if(!seen.Add(f)||!File.Exists(source))throw new Exception("Thiếu tệp hoặc đường dẫn trùng trong cập nhật.");journal.Add(new XElement("file",new XAttribute("path",f),new XAttribute("old",File.Exists(dest)?"1":"0"),new XAttribute("ready","1"),new XAttribute("newHash",Hash(source))));}
  SaveJournal(journalPath,journal);
  try{int i=0;foreach(var row in journal.Elements("file")){string relative=A(row,"path"),dest=Safe(target,relative),source=Safe(stage,relative),old=Safe(backup,relative);
    if(A(row,"old")=="1"){Directory.CreateDirectory(Path.GetDirectoryName(old));File.Move(dest,old);}Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Move(source,dest);i++;if(fault!=null)fault(i);if(i%100==0)notify("Đã cập nhật "+i+" / "+files.Count+" tệp");
   }File.Delete(journalPath);
  }catch{Recover(target);throw;}
  try{Directory.Delete(backup,true);}catch{}
 }
 public static void Apply(string package,string target,Action<string> notify){
  target=Path.GetFullPath(target);string app=Path.Combine(target,"V15VoiceStudio.exe");if(!File.Exists(Path.Combine(target,"ai","runtime","python.exe"))||!Directory.Exists(Path.Combine(target,"ai","models")))throw new Exception("Chọn đúng thư mục đã cài V15 1.4 đầy đủ, có V15VoiceStudio.exe và thư mục ai.");
  using(var updateLock=new FileStream(Path.Combine(target,".v15-update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   try{if(File.Exists(app))using(var check=new FileStream(app,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){} }catch{throw new Exception("Đóng V15 Voice Studio trước khi cập nhật.");}
   Recover(target);
   if(!File.Exists(app))throw new Exception("Không tìm thấy app sau khi khôi phục; chọn đúng thư mục cài.");
   Version baseVersion;if(!Version.TryParse(FileVersionInfo.GetVersionInfo(app).FileVersion,out baseVersion)||baseVersion<new Version(1,4,0,0))throw new Exception("Bạn đang chọn thư mục bản cũ hơn 1.4. Hãy chọn bản 1.4 đã cài đầy đủ.");
   foreach(string oldStage in Directory.GetDirectories(target,".v15-stage-*").Concat(Directory.GetDirectories(target,".v15-backup-*"))){string marker=Path.Combine(oldStage,".v15-owner");if((File.GetAttributes(oldStage)&FileAttributes.ReparsePoint)==0&&File.Exists(marker)&&File.ReadAllText(marker)=="V15VoiceStudio")Directory.Delete(oldStage,true);}
   var header=Manifest(package);long space;if(!Int64.TryParse(A(header,"space"),out space)||space<0)throw new Exception("Gói thiếu thông tin dung lượng.");
   var drive=new DriveInfo(Path.GetPathRoot(target));if(drive.AvailableFreeSpace<space)throw new Exception("Cần thêm "+Math.Ceiling(space/1073741824.0)+" GB trống trên ổ cập nhật.");
   if(A(header,"kind")=="gpu"){Version version;string text=FileVersionInfo.GetVersionInfo(app).FileVersion;if(!Version.TryParse(text,out version)||version<new Version(1,4,1,0))throw new Exception("Cập nhật ZIP ứng dụng 1.4.1 trước, sau đó thêm gói GPU.");}
   if(A(header,"kind")=="onnx"&&baseVersion<new Version(1,4,3,0))throw new Exception("Cập nhật ZIP ứng dụng 1.4.3 trước, sau đó cài gói GPU ONNX.");
   string stage=Path.Combine(target,".v15-stage-"+Guid.NewGuid().ToString("N"));
   try{Directory.CreateDirectory(stage);File.WriteAllText(Path.Combine(stage,".v15-owner"),"V15VoiceStudio");notify("Kiểm tra gói cập nhật…");var files=Stage(package,stage,notify);notify("Đang sao lưu và thay tệp. Vui lòng chờ…");if(A(header,"kind")=="core"){
     Version installed,next,expected;if(!Version.TryParse(FileVersionInfo.GetVersionInfo(app).FileVersion,out installed)||!Version.TryParse(FileVersionInfo.GetVersionInfo(Path.Combine(stage,"V15VoiceStudio.exe")).FileVersion,out next)||!Version.TryParse(A(header,"version"),out expected))throw new Exception("Không đọc được phiên bản EXE trong ZIP.");
     if(next<installed||next.Major!=expected.Major||next.Minor!=expected.Minor||next.Build!=expected.Build)throw new Exception("Phiên bản trong ZIP không khớp hoặc cũ hơn app đang cài.");
    }Commit(stage,target,files,notify);
    if(Environment.OSVersion.Platform==PlatformID.Win32NT){try{using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio")){key.SetValue("DisplayVersion",FileVersionInfo.GetVersionInfo(app).FileVersion);key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",app);}}catch{}}
   }finally{if(Directory.Exists(stage))try{Directory.Delete(stage,true);}catch{}}
  }
 }
}
public class UpdateForm:Form {
 readonly Label label=new Label();readonly Button start=new Button();readonly TextBox package=new TextBox(),target=new TextBox();bool running;readonly bool restart;readonly int parent;
 public UpdateForm(string initialPackage,string initialTarget,bool reopen,int parentId){restart=reopen;parent=parentId;Text="V15 · Cập nhật ZIP 1.4.6";Size=new Size(740,340);MinimumSize=Size;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(layout);
  layout.Controls.Add(new Label{Text="CẬP NHẬT V15 TỪ ZIP · giữ model, WAV và giọng riêng",AutoSize=true});package.Width=670;package.ReadOnly=true;package.Text=initialPackage;layout.Controls.Add(package);var pick=new Button{Text="Chọn ZIP cập nhật",AutoSize=true};pick.Click+=(s,e)=>{using(var d=new OpenFileDialog{Filter="V15 ZIP|*.zip"})if(d.ShowDialog()==DialogResult.OK)package.Text=d.FileName;};layout.Controls.Add(pick);target.Width=670;target.ReadOnly=true;target.Text=initialTarget;layout.Controls.Add(target);var folder=new Button{Text="Chọn thư mục V15 đã cài",AutoSize=true};folder.Click+=(s,e)=>{using(var d=new FolderBrowserDialog{SelectedPath=target.Text})if(d.ShowDialog()==DialogResult.OK)target.Text=d.SelectedPath;};layout.Controls.Add(folder);start.Text="CẬP NHẬT";start.AutoSize=true;layout.Controls.Add(start);label.MaximumSize=new Size(660,0);label.AutoSize=true;layout.Controls.Add(label);
  start.Click+=async(s,e)=>{if(running)return;running=true;start.Enabled=pick.Enabled=folder.Enabled=false;string src=package.Text,dest=target.Text;try{await Task.Run(()=>{if(parent>0)try{var p=Process.GetProcessById(parent);if(!p.WaitForExit(30000))throw new Exception("App chưa đóng. Đóng app rồi thử lại.");}catch(ArgumentException){}UpdateEngine.Apply(src,dest,message=>BeginInvoke((Action)(()=>label.Text=message)));});label.Text="Cập nhật hoàn tất. Mở V15VoiceStudio.exe tại thư mục vừa chọn.";if(restart){Process.Start(Path.Combine(dest,"V15VoiceStudio.exe"));running=false;Close();}}catch(Exception ex){label.Text="Chưa cập nhật: "+ex.Message;MessageBox.Show(this,label.Text,"V15",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{running=false;if(!IsDisposed)start.Enabled=pick.Enabled=folder.Enabled=true;}};
  FormClosing+=(s,e)=>{if(running){e.Cancel=true;label.Text="Đang cập nhật. Vui lòng chờ để bảo toàn dữ liệu.";}};
  if(parent>0)Shown+=(s,e)=>start.PerformClick();
 }
}
public static class Program {
 [STAThread]public static void Main(string[] args){string package="",target="";int parent=0;bool restart=args.Contains("--restart");for(int i=0;i<args.Length-1;i++){if(args[i]=="--zip"||args[i]=="--folder")package=args[i+1];if(args[i]=="--target")target=args[i+1];if(args[i]=="--parent")Int32.TryParse(args[i+1],out parent);}
  if(args.Contains("--apply-cli")){try{UpdateEngine.Apply(package,target,Console.WriteLine);Environment.Exit(0);}catch(Exception e){Console.Error.WriteLine(e.Message);Environment.Exit(1);}return;}
  if(String.IsNullOrWhiteSpace(target)&&Environment.OSVersion.Platform==PlatformID.Win32NT)try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio"))if(key!=null)target=Convert.ToString(key.GetValue("InstallLocation",""));}catch{}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new UpdateForm(package,target,restart,parent));
 }
}
}
