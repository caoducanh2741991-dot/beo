using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace V15Voice {
public sealed class CreatorProject {
 public string Id=Guid.NewGuid().ToString("N"),Video="",SourceSrt="",Voice="",FinalSrt="",OutputFolder="",Caption="",State="QUEUED",Error="",Output="",Zip="",BundleHash="",InputKey="",ReviewedPairKey="";
 public bool Selected=true,RequireVietnamese=true,UserReviewed=false,ExportMp4=false,BurnCaptions=false,Vertical=false,MixOriginal=false;
 public double OriginalGain=.12;
 public TranslationResult Translation;
 public readonly RenderProgress Progress=new RenderProgress();
 public string Name {get{return Path.GetFileNameWithoutExtension(Video);}}
 public void AcknowledgePair(Func<bool> cancel){ReviewedPairKey=SpeechArchive.Hash(CreatorFiles.Hash(Voice,cancel)+"|"+CreatorFiles.Hash(FinalSrt,cancel));UserReviewed=true;}
 public static CreatorProject FromVideo(string file){
  string full=Path.GetFullPath(file),stem=Path.Combine(Path.GetDirectoryName(full),Path.GetFileNameWithoutExtension(full));
  var p=new CreatorProject{Video=full,OutputFolder=Path.Combine(Path.GetDirectoryName(full),"BEO_CREATOR")};
  if(File.Exists(stem+".srt"))p.SourceSrt=stem+".srt";
  if(File.Exists(stem+".wav"))p.Voice=stem+".wav";
  if(File.Exists(stem+".final.srt"))p.FinalSrt=stem+".final.srt";
  return p;
 }
 public string Fingerprint(Func<bool> cancel){
  var files=new[]{Video,SourceSrt,Voice,FinalSrt};var b=new StringBuilder("creator400-v1|");
  foreach(string file in files){if(cancel())throw new OperationCanceledException();b.Append(file).Append('|').Append(File.Exists(file)?CreatorFiles.Hash(file,cancel):"missing").Append('|');}
  b.Append(RequireVietnamese).Append(UserReviewed).Append(ReviewedPairKey).Append(ExportMp4).Append(BurnCaptions).Append(Vertical).Append(MixOriginal).Append(OriginalGain.ToString("R",CultureInfo.InvariantCulture)).Append(Caption);
  return SpeechArchive.Hash(b.ToString());
 }
 public XElement Xml(){
  var x=new XElement("clip",new XAttribute("id",Id),new XAttribute("selected",Selected),new XAttribute("vi",RequireVietnamese),new XAttribute("reviewed",UserReviewed),new XAttribute("mp4",ExportMp4),new XAttribute("burn",BurnCaptions),new XAttribute("vertical",Vertical),new XAttribute("mix",MixOriginal),new XAttribute("gain",OriginalGain.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("state",State),new XAttribute("progress",Progress.Read().Percent));
  foreach(var a in new[]{new[]{"video",Video},new[]{"source",SourceSrt},new[]{"voice",Voice},new[]{"srt",FinalSrt},new[]{"folder",OutputFolder},new[]{"caption",Caption},new[]{"error",Error},new[]{"output",Output},new[]{"zip",Zip},new[]{"bundleHash",BundleHash},new[]{"inputKey",InputKey}})x.Add(new XElement(a[0],a[1]??""));
  x.Add(new XElement("reviewedPairKey",ReviewedPairKey));
  if(Translation!=null){var t=new XElement("translation",new XAttribute("status",Translation.Status),new XAttribute("sourceHash",Translation.SourceHash),new XAttribute("model",Translation.Model),new XAttribute("error",Translation.Error));foreach(var c in Translation.Text)t.Add(new XElement("cue",new XAttribute("id",c.Key),c.Value));x.Add(t);}return x;
 }
 static bool B(XElement x,string n,bool fallback=false){bool b;return Boolean.TryParse((string)x.Attribute(n),out b)?b:fallback;}
 static string S(XElement x,string n){return (string)x.Element(n)??"";}
 public static CreatorProject From(XElement x){
  var p=new CreatorProject{Id=(string)x.Attribute("id")??Guid.NewGuid().ToString("N"),Selected=B(x,"selected",true),RequireVietnamese=B(x,"vi",true),UserReviewed=B(x,"reviewed"),ExportMp4=B(x,"mp4"),BurnCaptions=B(x,"burn"),Vertical=B(x,"vertical"),MixOriginal=B(x,"mix"),Video=S(x,"video"),SourceSrt=S(x,"source"),Voice=S(x,"voice"),FinalSrt=S(x,"srt"),OutputFolder=S(x,"folder"),Caption=S(x,"caption"),State=(string)x.Attribute("state")??"QUEUED",Error=S(x,"error"),Output=S(x,"output"),Zip=S(x,"zip"),BundleHash=S(x,"bundleHash"),InputKey=S(x,"inputKey")};
  double g;if(Double.TryParse((string)x.Attribute("gain"),NumberStyles.Float,CultureInfo.InvariantCulture,out g)&&!Double.IsNaN(g)&&g>=0&&g<=.3)p.OriginalGain=g;
  p.ReviewedPairKey=S(x,"reviewedPairKey");
  if(p.State=="RUNNING")p.State="INTERRUPTED";
  int percent;Int32.TryParse((string)x.Attribute("progress"),out percent);p.Progress.Restore(percent,0,p.State=="PACKAGE_EXPORTED"||p.State=="MP4_EXPORTED");
  var t=x.Element("translation");if(t!=null){p.Translation=new TranslationResult{Status=(string)t.Attribute("status")??"TRANSLATION_INCOMPLETE",SourceHash=(string)t.Attribute("sourceHash")??"",Model=(string)t.Attribute("model")??"",Error=(string)t.Attribute("error")??""};foreach(var c in t.Elements("cue")){int id;if(Int32.TryParse((string)c.Attribute("id"),out id)&&!p.Translation.Text.ContainsKey(id))p.Translation.Text[id]=c.Value;}}
  return p;
 }
}
public static class CreatorStore {
 public static List<CreatorProject> Load(string path){var x=StateFiles.Read(path);return x==null||x.Name!="creator-projects"?new List<CreatorProject>():x.Elements("clip").Take(500).Select(CreatorProject.From).ToList();}
 public static void Save(string path,IEnumerable<CreatorProject> clips){StateFiles.Write(path,new XElement("creator-projects",new XAttribute("version","4.0.0"),clips.Select(p=>p.Xml())));}
}
public static class CreatorFiles {
 public static string Hash(string file,Func<bool> cancel){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(file)){var b=new byte[1024*1024];int n;while((n=s.Read(b,0,b.Length))>0){if(cancel())throw new OperationCanceledException();h.TransformBlock(b,0,n,null,0);}h.TransformFinalBlock(b,0,0);return BitConverter.ToString(h.Hash).Replace("-","").ToLowerInvariant();}}
 public static void Copy(string source,string target,Func<bool> cancel){using(var i=File.OpenRead(source))using(var o=new FileStream(target,FileMode.CreateNew,FileAccess.Write)){var b=new byte[1024*1024];int n;while((n=i.Read(b,0,b.Length))>0){if(cancel())throw new OperationCanceledException();o.Write(b,0,n);}}}
 public static string ReceiptFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"V15VoiceStudio","voice-receipts400");}}
}
// This is evidence of the exact TTS text used by our publisher, not ASR or forced alignment.
public static class CreatorReceipt {
 public static string NameFor(string wavePath,string srtHash){return SpeechArchive.Hash(Path.GetFullPath(wavePath)+"|"+srtHash)+".xml";}
 public static string TextKey(List<Cue> cues){return SpeechArchive.Hash(System.Text.RegularExpressions.Regex.Replace(String.Join(" ",cues.Select(c=>c.Text)),@"\s+"," ").Trim());}
 public static void Record(WorkflowBundle b,VoiceRenderReport report,RenderOptions o){
  var cues=Scripts.ParseSrt(File.ReadAllText(b.Srt));StableArtifacts.VerifyWords(cues,report);
  string hash=CreatorFiles.Hash(b.Wave,()=>false),srtHash=CreatorFiles.Hash(b.Srt,()=>false);var x=new XElement("voice-receipt",new XAttribute("version","400-v3"),new XAttribute("wavePath",Path.GetFullPath(b.Wave)),new XAttribute("wave",hash),new XAttribute("srt",srtHash),new XAttribute("text",TextKey(cues)),new XAttribute("timeline",o.Anchored||o.Locked?"source":"render"),new XAttribute("mode",o.WorkflowMode));
  foreach(string p in b.Reports)x.Add(new XElement("report",new XAttribute("path",Path.GetFullPath(p)),new XAttribute("hash",CreatorFiles.Hash(p,()=>false))));
  string dir=Path.Combine(CreatorFiles.ReceiptFolder,hash);Directory.CreateDirectory(dir);StateFiles.Write(Path.Combine(dir,NameFor(b.Wave,srtHash)),x);
 }
 public static XElement Read(string waveHash,string srtHash,string wavePath){string dir=Path.Combine(CreatorFiles.ReceiptFolder,waveHash);if(!Directory.Exists(dir))return null;var x=StateFiles.Read(Path.Combine(dir,NameFor(wavePath,srtHash)));if(x==null)x=Directory.GetFiles(dir,"*.xml").Select(StateFiles.Read).FirstOrDefault(r=>r!=null&&(string)r.Attribute("wavePath")==Path.GetFullPath(wavePath));return x!=null&&x.Name=="voice-receipt"&&(string)x.Attribute("version")=="400-v3"?x:null;}
}
}
