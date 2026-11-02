using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using System.Security.Cryptography;

namespace V15Voice {
public static class StateFiles {
 public static void Write(string path,XElement root){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{new XDocument(root).Save(temp);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static XElement Read(string path){foreach(string p in new[]{path,path+".bak"}){try{if(!File.Exists(p)||new FileInfo(p).Length>64*1024*1024)continue;var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null};using(var reader=XmlReader.Create(p,settings))return XElement.Load(reader);}catch{}}return null;}
 public static string Hash(string path){using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");}
 public static string S(XElement x,string k,string fallback=""){return (string)x.Attribute(k)??fallback;}
 public static int I(XElement x,string k,int d=0){int v;return int.TryParse(S(x,k),out v)?v:d;}
 public static double D(XElement x,string k,double d=0){double v;return double.TryParse(S(x,k),NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!Double.IsNaN(v)&&!Double.IsInfinity(v)?v:d;}
}
public sealed class VoiceSettings {
 public string Name="",Key="K:3",BreathPath="",Device="auto";public int Mode,Breathing,BreathEvery=3,BatchMode;public double Speed=1,MaxSpeed=1.3,Gap=.15,BreathLevel=16;public bool Slow=true,Join,Skip=true;public int Punctuation;public double CommaPause=.14,StopPause=.30,QuestionPause=.35,ParagraphPause=.50;
 public XElement Xml(){return new XElement("settings",new XAttribute("punctuation",Punctuation),new XAttribute("commaPause",CommaPause),new XAttribute("stopPause",StopPause),new XAttribute("questionPause",QuestionPause),new XAttribute("paragraphPause",ParagraphPause),new XAttribute("device",Device),new XAttribute("name",Name),new XAttribute("key",Key),new XAttribute("mode",Mode),new XAttribute("breathing",Breathing),new XAttribute("every",BreathEvery),new XAttribute("speed",Speed),new XAttribute("max",MaxSpeed),new XAttribute("gap",Gap),new XAttribute("level",BreathLevel),new XAttribute("slow",Slow?1:0),new XAttribute("join",Join?1:0),new XAttribute("batchMode",BatchMode),new XAttribute("skip",Skip?1:0),new XAttribute("breathPath",BreathPath));}
 public static VoiceSettings From(XElement x){return new VoiceSettings{Punctuation=StateFiles.I(x,"punctuation"),CommaPause=StateFiles.D(x,"commaPause",.14),StopPause=StateFiles.D(x,"stopPause",.30),QuestionPause=StateFiles.D(x,"questionPause",.35),ParagraphPause=StateFiles.D(x,"paragraphPause",.50),Device=StateFiles.S(x,"device","auto"),Name=StateFiles.S(x,"name"),Key=StateFiles.S(x,"key","K:3"),Mode=StateFiles.I(x,"mode"),Breathing=StateFiles.I(x,"breathing"),BreathEvery=StateFiles.I(x,"every",3),Speed=StateFiles.D(x,"speed",1),MaxSpeed=StateFiles.D(x,"max",1.3),Gap=StateFiles.D(x,"gap",.15),BreathLevel=StateFiles.D(x,"level",16),Slow=StateFiles.I(x,"slow",1)==1,Join=StateFiles.I(x,"join")==1,BatchMode=StateFiles.I(x,"batchMode"),Skip=StateFiles.I(x,"skip",1)==1,BreathPath=StateFiles.S(x,"breathPath")};}
 public override string ToString(){return Name;}
}
public static class QueueStore {
 public static void Save(string path,IEnumerable<BatchJob> jobs){
  var xml=new XElement("queue",new XAttribute("version",1));foreach(var j in jobs){var row=new XElement("job",new XAttribute("path",j.InputPath),new XAttribute("hash",j.SourceHash??""),new XAttribute("timed",j.Timed?1:0),new XAttribute("state",(int)j.State),new XAttribute("output",j.OutputPath??""),new XAttribute("error",j.Error??""));
   foreach(var c in (j.State==JobState.Running&&j.CheckpointCues!=null?j.CheckpointCues:j.Cues))row.Add(new XElement("cue",new XAttribute("start",c.Start),new XAttribute("end",c.End),c.Text??""));xml.Add(row);
  }StateFiles.Write(path,xml);
 }
 public static List<BatchJob> Load(string path){var jobs=new List<BatchJob>();var xml=StateFiles.Read(path);if(xml==null)return jobs;
  foreach(var row in xml.Elements("job").Take(500)){
   string input=StateFiles.S(row,"path");if(String.IsNullOrWhiteSpace(input))continue;var fresh=BatchJob.Load(input);
   if(fresh.State==JobState.ImportFailed){jobs.Add(fresh);continue;}
   if(String.IsNullOrEmpty(StateFiles.S(row,"hash"))||fresh.SourceHash!=StateFiles.S(row,"hash")){fresh.Error="File nguồn đã thay đổi; đã nhập lại để tạo bản mới.";jobs.Add(fresh);continue;}
   try{var cues=row.Elements("cue").Select(c=>new Cue{Start=StateFiles.D(c,"start"),End=StateFiles.D(c,"end"),Text=c.Value}).ToList();Scripts.Validate(cues);fresh.Cues=cues;fresh.Timed=StateFiles.I(row,"timed")==1;int state=StateFiles.I(row,"state");fresh.State=Enum.IsDefined(typeof(JobState),state)?(JobState)state:JobState.Queued;fresh.OutputPath=StateFiles.S(row,"output");fresh.Error=StateFiles.S(row,"error");
    if(fresh.State==JobState.Running||fresh.State==JobState.Cancelled)fresh.State=JobState.Queued;
    if(fresh.State==JobState.Done&&!File.Exists(fresh.OutputPath)){fresh.State=JobState.Queued;fresh.Error="WAV cũ không còn; cần tạo lại.";}
   }catch{fresh=BatchJob.Load(input);}jobs.Add(fresh);
  }return jobs;
 }
}
public static class SpeechGroups {
 public static List<Cue> Join(List<Cue> source){var result=new List<Cue>();foreach(var c in source){Cue prior=result.LastOrDefault();if(prior!=null&&!System.Text.RegularExpressions.Regex.IsMatch(prior.Text.TrimEnd(),"[.!?][\"”')\\]]?$")&&c.Start-prior.End<=.25&&prior.Words+c.Words<=40&&prior.Text.Length+c.Text.Length<1900){prior.Text+=" "+c.Text;prior.End=c.End;}else result.Add(new Cue{Id=result.Count+1,Start=c.Start,End=c.End,Text=c.Text});}return result;}
 public static string Inspect(List<Cue> cues){var lines=new List<string>();for(int i=0;i<cues.Count;i++){var c=cues[i];var issues=new List<string>();if(c.Words>10)issues.Add(">10 từ");if(c.Words/c.Duration>3.2)issues.Add("khung đọc gấp");if(c.Words/c.Duration<1.5)issues.Add("có thể dư khoảng trống");if(i>0&&c.Start-cues[i-1].End>.5)issues.Add("nghỉ "+(c.Start-cues[i-1].End).ToString("0.00")+"s trước câu");if(issues.Count>0)lines.Add("#"+c.Id+" · "+Scripts.Stamp(c.Start)+" · "+String.Join(", ",issues));}return "Kiểm tra sơ bộ: "+cues.Count+" block, "+cues.Sum(c=>c.Words)+" từ.\r\nĐây là ước lượng từ văn bản; thời lượng chính xác được kiểm tra khi tạo WAV.\r\n\r\n"+(lines.Count==0?"Chưa thấy cảnh báo từ văn bản.":String.Join("\r\n",lines));}
}
}
