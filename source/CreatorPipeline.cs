using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace V15Voice {
public sealed class CreatorCheck {public string Level,Code,Text;public int Cue;}
public sealed class CreatorAudit {
 public CreatorMediaInfo Video;public double VoiceSeconds;public string WaveHash="",SrtHash="",Sync="SYNC_UNVERIFIED";public XElement Receipt;
 public List<Cue> Cues=new List<Cue>();public List<CreatorCheck> Checks=new List<CreatorCheck>();
 public bool CanExport{get{return Checks.All(c=>c.Level!="ERROR");}}
 public void Add(string level,string code,string text,int cue=0){Checks.Add(new CreatorCheck{Level=level,Code=code,Text=text,Cue=cue});}
 public string Summary(){return String.Join("\r\n",Checks.Select(c=>c.Level+" · "+c.Code+(c.Cue>0?" · câu "+c.Cue:"")+" · "+c.Text));}
}
public static class CreatorPipeline {
 public static bool TranslationReady(CreatorProject p,List<Cue> cues){
  var t=p.Translation;if(t==null||!t.Complete||t.SourceHash!=AiRepairSession.SnapshotHash(cues)||t.Text.Count!=cues.Count)return false;
  try{foreach(var c in cues)OfflineTranslation.Validate(c.Text,t.Text[c.Id]);return true;}catch{return false;}
 }
 public static CreatorAudit Inspect(string root,CreatorProject p,Func<bool> cancel){
  var a=new CreatorAudit();
  try{a.Video=CreatorMedia.Probe(root,p.Video,cancel);a.Add("INFO","VIDEO_MEASURED",a.Video.Width+" × "+a.Video.Height+" · "+a.Video.Duration.ToString("0.000",CultureInfo.InvariantCulture)+"s · "+a.Video.Codec+" · "+a.Video.Fps.ToString("0.###",CultureInfo.InvariantCulture)+" fps");
   a.Add(a.Video.Duration>60?"INFO":"WARN","CREATOR_DURATION",a.Video.Duration>60?"Video dài hơn 1 phút. Đây chỉ là phép kiểm tra thời lượng.":"Video không dài hơn 1 phút; không tự kéo dài hình hoặc chèn im lặng.");
   if(Math.Abs(a.Video.Width/(double)a.Video.Height-9/16.0)>.015)a.Add("WARN","FRAME_REVIEW","Khung hình chưa phải 9:16. Có thể chọn thêm viền dọc; không tự cắt nội dung.");
   if(a.Video.Fps<23||a.Video.Fps>60)a.Add("WARN","FPS_REVIEW","FPS ngoài mốc 23–60 của hướng dẫn API TikTok; cần kiểm tra khi dựng/xuất.");
   if(a.Video.Rotation.Length>0)a.Add("WARN","ROTATION_METADATA",a.Video.Rotation+"; kích thước trên là trước khi xoay.");
  }catch(OperationCanceledException){throw;}catch(Exception ex){a.Add("ERROR","VIDEO_ERROR",ex.Message);}
  try{if(!File.Exists(p.FinalSrt))throw new IOException("Chưa gắn SRT cuối đã dùng tạo voice.");a.Cues=Scripts.ParseSrt(File.ReadAllText(p.FinalSrt));a.SrtHash=CreatorFiles.Hash(p.FinalSrt,cancel);a.Add("INFO","SRT_PARSED",a.Cues.Count+" cue · kết thúc "+Scripts.Stamp(a.Cues.Last().End)+" · mốc SRT không phải forced alignment");
   foreach(var c in a.Cues){int chars=c.Text.Count(ch=>!Char.IsWhiteSpace(ch));if(chars/c.Duration>20)a.Add("WARN","CAPTION_FAST",(chars/c.Duration).ToString("0.0",CultureInfo.InvariantCulture)+" ký tự/giây; cần đọc lại phụ đề.",c.Id);if(CreatorCaptions.Lines(c.Text).Length>2)a.Add(p.BurnCaptions?"ERROR":"WARN","CAPTION_LONG","Vượt 2 dòng ở kiểu chữ mặc định; SRT rời vẫn giữ đầy đủ lời.",c.Id);}
   if(a.Video!=null&&a.Cues.Last().End>a.Video.Duration+.015)a.Add("ERROR","SRT_OVERRUN","SRT vượt hình "+(a.Cues.Last().End-a.Video.Duration).ToString("0.000",CultureInfo.InvariantCulture)+"s; không tự cắt câu.");
   if(File.Exists(p.SourceSrt)){var original=Scripts.ParseSrt(File.ReadAllText(p.SourceSrt));bool same=original.Count==a.Cues.Count&&original.Zip(a.Cues,(x,y)=>x.Start==y.Start&&x.End==y.End).All(v=>v);a.Add(same?"INFO":"WARN","SOURCE_TIMELINE",same?"Mốc nguồn và SRT cuối khớp từng cue.":"SRT cuối khác mốc nguồn; kiểm tra chế độ linh hoạt/căn theo voice.");}
  }catch(OperationCanceledException){throw;}catch(Exception ex){a.Add("ERROR","SRT_ERROR",ex.Message);}
  try{a.VoiceSeconds=FinalAudio.PcmFrames(p.Voice)/24000.0;a.WaveHash=CreatorFiles.Hash(p.Voice,cancel);a.Add("INFO","WAV_MEASURED",a.VoiceSeconds.ToString("0.000",CultureInfo.InvariantCulture)+"s · PCM mono 24 kHz/16-bit");
   if(a.Video!=null){double delta=a.VoiceSeconds-a.Video.Duration;a.Add(Math.Abs(delta)>.15?"ERROR":"INFO","DURATION_SYNC","WAV − video = "+delta.ToString("+0.000;-0.000;0.000",CultureInfo.InvariantCulture)+"s · giới hạn ghép 0,150s. Không đổi tốc độ hình/voice.");}
   a.Receipt=CreatorReceipt.Read(a.WaveHash,a.SrtHash,p.Voice);
   if(a.Receipt!=null){if((string)a.Receipt.Attribute("wave")!=a.WaveHash||(string)a.Receipt.Attribute("srt")!=a.SrtHash||(string)a.Receipt.Attribute("text")!=CreatorReceipt.TextKey(a.Cues)){a.Sync="SYNC_MISMATCH";a.Add("ERROR",a.Sync,"Hash WAV/SRT/lời TTS không khớp biên nhận lần render; không cho bỏ qua bằng tick duyệt.");}
    else{a.Sync="TTS_TEXT_VERIFIED";a.Add("INFO",a.Sync,"SRT khớp chính xác lời đã gửi TTS và hash WAV/SRT của lần render 4.0. Chưa xác nhận phát âm bằng ASR.");}}
   else{bool reviewed=p.UserReviewed&&p.ReviewedPairKey==SpeechArchive.Hash(a.WaveHash+"|"+a.SrtHash);a.Sync=reviewed?"USER_REVIEWED":"SYNC_UNVERIFIED";a.Add(reviewed?"WARN":"ERROR",a.Sync,reviewed?"Cặp nhập ngoài do người dùng xác nhận nghe/đối chiếu; app chưa chứng minh lời trong audio.":"WAV từ bản cũ/nhập ngoài: chưa có biên nhận TTS hoặc cặp đã đổi sau khi duyệt. Nghe và đối chiếu rồi tick lại xác nhận, hoặc render lại bằng 4.0.");}
  }catch(OperationCanceledException){throw;}catch(Exception ex){a.Add("ERROR","WAV_ERROR",ex.Message);}
  if(p.ExportMp4)try{var tools=CreatorMedia.Capabilities(root,cancel);if(!tools.Mux)a.Add("ERROR","CREATOR_TOOLS_REQUIRED","Cần FFmpeg video đầy đủ có AAC và MP4. Nhập ở Thành phần video, hoặc chọn Gói dựng.");if((p.BurnCaptions||p.Vertical||(a.Video!=null&&a.Video.Codec!="h264"))&&!System.Text.RegularExpressions.Regex.IsMatch(tools.Encoder,@"\blibx264\b"))a.Add("ERROR","VIDEO_ENCODER_MISSING","Tùy chọn hình hiện tại cần libx264.");if(p.BurnCaptions&&!tools.Draw)a.Add("ERROR","CAPTION_ENCODER_MISSING","In chữ cần libass/ass và libx264.");}catch(OperationCanceledException){throw;}catch(Exception ex){a.Add("ERROR","CREATOR_TOOLS_ERROR",ex.Message);}
  a.Add(p.RequireVietnamese&&!TranslationReady(p,a.Cues)?"WARN":"INFO",TranslationReady(p,a.Cues)?"TRANSLATION_COMPLETE":p.RequireVietnamese?"TRANSLATION_PENDING":"VI_NOT_REQUESTED",p.RequireVietnamese?"Xuất gói sẽ dịch đủ cue bằng Qwen offline và kiểm tra; thiếu bản dịch sẽ dừng gói này.":"Đã chọn gói không kèm đối chiếu Việt.");
  if(p.MixOriginal)a.Add("WARN","ORIGINAL_AUDIO_MIX","Trộn âm thanh gốc "+(p.OriginalGain*100).ToString("0")+"%. Bao gồm cả lời thoại/nhạc nếu có; không phải tách riêng SFX.");
  a.Add("WARN","CONTENT_REVIEW","Cần tự xác nhận nguồn hình/nhạc và tính nguyên gốc. Kiểm tra kỹ thuật không xác nhận điều kiện kiếm tiền.");return a;
 }
 public static bool Reusable(CreatorProject p,Func<bool> cancel){
  if(!(p.State=="PACKAGE_EXPORTED"||p.State=="MP4_EXPORTED")||!File.Exists(p.Zip)||p.BundleHash.Length==0||CreatorFiles.Hash(p.Zip,cancel)!=p.BundleHash||p.InputKey!=p.Fingerprint(cancel))return false;
  try{using(var zip=ZipFile.OpenRead(p.Zip)){var entry=zip.GetEntry(p.Name+".creator.xml");if(entry==null)return false;XElement manifest;var settings=new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null};using(var reader=System.Xml.XmlReader.Create(entry.Open(),settings))manifest=XElement.Load(reader);string folder=Path.GetDirectoryName(p.Zip);foreach(var file in manifest.Elements("file")){string name=(string)file.Attribute("name");if(String.IsNullOrEmpty(name)||name!=Path.GetFileName(name))return false;string path=Path.Combine(folder,name);if(!File.Exists(path)||CreatorFiles.Hash(path,cancel)!=(string)file.Attribute("sha256"))return false;}if(p.RequireVietnamese&&!TranslationReady(p,Scripts.ParseSrt(File.ReadAllText(p.FinalSrt))))return false;}return true;}catch(OperationCanceledException){throw;}catch{return false;}
 }
 static void Archive(string directory,string output,Func<bool> cancel){using(var zip=ZipFile.Open(output,ZipArchiveMode.Create))foreach(string file in Directory.GetFiles(directory).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase)){if(cancel())throw new OperationCanceledException();var entry=zip.CreateEntry(Path.GetFileName(file),CompressionLevel.Fastest);using(var i=File.OpenRead(file))using(var o=entry.Open()){var b=new byte[1024*1024];int n;while((n=i.Read(b,0,b.Length))>0){if(cancel())throw new OperationCanceledException();o.Write(b,0,n);}}}}
 static void VerifyArchive(string path,string directory,Func<bool> cancel){using(var z=ZipFile.OpenRead(path)){var expected=Directory.GetFiles(directory);if(z.Entries.Count!=expected.Length||z.Entries.Any(e=>e.FullName!=Path.GetFileName(e.FullName))||z.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=expected.Length)throw new IOException("PACKAGE_VERIFY_FAILED: cấu trúc ZIP không hợp lệ.");foreach(var file in expected){if(cancel())throw new OperationCanceledException();using(var stream=z.GetEntry(Path.GetFileName(file)).Open())using(var sha=System.Security.Cryptography.SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(hash!=CreatorFiles.Hash(file,cancel))throw new IOException("PACKAGE_VERIFY_FAILED: dữ liệu ZIP sai hash.");}}}}
 public static CreatorAudit Export(string root,CreatorProject p,string translationCache,ITextRewriter translator,Func<bool> cancel,Action<int,string> progress,Action checkpoint=null,Action<string> fault=null){
  string work="";try{
   if(Reusable(p,cancel)){progress(100,"Dùng lại gói đã kiểm tra · đầu vào/options không đổi");return Inspect(root,p,cancel);}
   p.State="RUNNING";p.Error="";if(checkpoint!=null)checkpoint();progress(1,"Đo video, WAV và kiểm tra SRT");
   string input=p.Fingerprint(cancel);var audit=Inspect(root,p,cancel);if(!audit.CanExport)throw new IOException("PREFLIGHT_FAILED\r\n"+audit.Summary());progress(8,"Đã kiểm tra đầu vào · "+audit.Sync);
   if(p.RequireVietnamese&&!TranslationReady(p,audit.Cues)){p.Translation=OfflineTranslation.Run(audit.Cues,translator,translationCache,cancel,(n,m)=>progress(8+n*12/100,m),p.FinalSrt);if(checkpoint!=null)checkpoint();if(!TranslationReady(p,audit.Cues))throw new IOException("TRANSLATION_INCOMPLETE: "+p.Translation.Error);}
   audit.Checks.RemoveAll(c=>c.Code=="TRANSLATION_PENDING");if(p.RequireVietnamese)audit.Add("INFO","TRANSLATION_COMPLETE","Đủ bản dịch khớp EN cuối; đã kiểm tra cấu trúc/số liệu và đối chiếu nghĩa bằng model. Cần đọc lại chất lượng.");
   if(cancel())throw new OperationCanceledException();if(String.IsNullOrWhiteSpace(p.OutputFolder))throw new IOException("OUTPUT_REQUIRED: chọn thư mục xuất.");
   Directory.CreateDirectory(p.OutputFolder);work=Path.Combine(p.OutputFolder,".beo_creator_"+Guid.NewGuid().ToString("N"));string bundle=Path.Combine(work,"bundle");Directory.CreateDirectory(bundle);string stem=Path.Combine(bundle,p.Name);
   if(p.ExportMp4)CreatorMedia.Render(root,p,audit,stem+".mp4",work,cancel,progress);
   else{progress(25,"Đóng gói video gốc + voice rời để dựng");CreatorFiles.Copy(p.Video,stem+Path.GetExtension(p.Video).ToLowerInvariant(),cancel);}
   progress(88,"Đồng bộ WAV · SRT · đối chiếu Việt");CreatorFiles.Copy(p.Voice,stem+".wav",cancel);CreatorFiles.Copy(p.FinalSrt,stem+".final.srt",cancel);if(CreatorFiles.Hash(stem+".wav",cancel)!=audit.WaveHash||CreatorFiles.Hash(stem+".final.srt",cancel)!=audit.SrtHash)throw new IOException("SYNC_CHANGED: WAV/SRT đổi trong lúc sao chép.");
   if(p.RequireVietnamese){if(!TranslationReady(p,audit.Cues))throw new IOException("TRANSLATION_INCOMPLETE: bản Việt không khớp snapshot cuối.");File.WriteAllText(stem+".final.vi.txt",StableArtifacts.Bilingual(audit.Cues,true,false,p.Translation).Replace("V15 2.0.16", "BÉO Voice Studio 4.0"),new UTF8Encoding(false));}
   if(!String.IsNullOrWhiteSpace(p.Caption))File.WriteAllText(stem+".caption.txt",p.Caption.Trim()+"\r\n",new UTF8Encoding(false));
   if(audit.Receipt!=null&&audit.Sync=="TTS_TEXT_VERIFIED")foreach(var report in audit.Receipt.Elements("report")){string path=(string)report.Attribute("path")??"";if(!File.Exists(path)||CreatorFiles.Hash(path,cancel)!=(string)report.Attribute("hash"))throw new IOException("REPORT_CHANGED: báo cáo lần render đã đổi/mất. Render lại để có bộ nhất quán.");string suffix=path.EndsWith(".ai-before-after-report.txt",StringComparison.OrdinalIgnoreCase)?".ai-before-after-report.txt":path.EndsWith(".ai-changes-en-vi.txt",StringComparison.OrdinalIgnoreCase)?".ai-changes-en-vi.txt":".voice-report.txt";CreatorFiles.Copy(path,stem+suffix,cancel);}
   // The manifest describes provenance separately from audio, translation and publication status.
   var metadata=new XElement("creator-bundle",new XAttribute("version","4.0.0"),new XAttribute("kind",p.ExportMp4?"MP4":"EDITING_PACKAGE"),new XAttribute("sync",audit.Sync),new XAttribute("translation",p.RequireVietnamese?"TRANSLATION_COMPLETE":"VI_NOT_REQUESTED"),new XAttribute("contentEligibility","NOT_ASSESSED"),new XAttribute("cueTiming","SRT timeline; not measured forced alignment"),new XAttribute("wavMinusVideoSeconds",(audit.VoiceSeconds-audit.Video.Duration).ToString("R",CultureInfo.InvariantCulture)),new XAttribute("audioAdjustmentLimitSeconds","0.150"));foreach(string f in Directory.GetFiles(bundle))metadata.Add(new XElement("file",new XAttribute("name",Path.GetFileName(f)),new XAttribute("sha256",CreatorFiles.Hash(f,cancel))));metadata.Save(stem+".creator.xml");
   if(fault!=null)fault("before_archive");progress(92,"Tạo ZIP phẳng · kiểm tra hash từng file");string archive=Path.Combine(work,p.Name+".creator.zip");Archive(bundle,archive,cancel);VerifyArchive(archive,bundle,cancel);
   if(p.Fingerprint(cancel)!=input)throw new IOException("INPUT_CHANGED: dữ liệu/options thay đổi trong lúc xuất; chưa công bố gói.");if(fault!=null)fault("before_commit");if(cancel())throw new OperationCanceledException();
   // Publish the complete directory in one move. Never replace a previous successful clip.
   string dest=Path.Combine(p.OutputFolder,p.Name);int i=2;while(Directory.Exists(dest)||File.Exists(dest))dest=Path.Combine(p.OutputFolder,p.Name+" ("+(i++)+")");
   CreatorFiles.Copy(archive,Path.Combine(bundle,p.Name+".creator.zip"),cancel);string hashZip=CreatorFiles.Hash(archive,cancel);if(cancel())throw new OperationCanceledException();Directory.Move(bundle,dest);
   p.Output=Path.Combine(dest,p.Name+(p.ExportMp4?".mp4":Path.GetExtension(p.Video).ToLowerInvariant()));p.Zip=Path.Combine(dest,p.Name+".creator.zip");p.BundleHash=hashZip;p.InputKey=input;p.State=p.ExportMp4?"MP4_EXPORTED":"PACKAGE_EXPORTED";p.Error="";if(checkpoint!=null)checkpoint();progress(100,p.State+" · "+audit.Sync);return audit;
  }catch(Exception ex){p.State=ex is OperationCanceledException?"CANCELLED":ex.Message.StartsWith("TRANSLATION_INCOMPLETE")?"TRANSLATION_INCOMPLETE":ex.Message.StartsWith("PREFLIGHT")?"NEEDS_REVIEW":"EXPORT_FAILED";p.Error=ex.ToString();if(checkpoint!=null)checkpoint();throw;}
  finally{if(work.Length>0)try{Directory.Delete(work,true);}catch{}}
 }
}
}
