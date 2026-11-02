using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using V15Voice;
public static class BatchTests {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 public static void Main(string[] args){
  string root=Path.Combine(Path.GetTempPath(),"v15_batch_tests_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"a"));Directory.CreateDirectory(Path.Combine(root,"b"));
  string pa=Path.Combine(root,"a","same.srt"),pb=Path.Combine(root,"b","same.txt"),pc=Path.Combine(root,"bad.srt");
  string srt="1\n00:00:00,000 --> 00:00:03,300\nHave you ever forgotten why you entered a room?\n";
  File.WriteAllText(pa,srt);File.WriteAllText(pb,"Your brain may have switched contexts at the doorway.");File.WriteAllText(pc,"not an SRT");
  var jobs=new List<BatchJob>{BatchJob.Load(pa),BatchJob.Load(pc),BatchJob.Load(pb)};
  Check(jobs[0].Timed&&!jobs[2].Timed&&jobs[1].State==JobState.ImportFailed,"Mixed SRT/TXT auto modes and malformed input captured per file");
  var paths=new List<string>();string dest=BatchRunner.Run(jobs,true,()=>false,(j,p,n)=>{paths.Add(p);return p;},(p,m)=>{});
  Check(jobs[0].State==JobState.Done&&jobs[2].State==JobState.Done&&paths.Count==2,"Bad import does not block later files");
  Check(paths[0]!=paths[1]&&paths[0]==Path.ChangeExtension(pa,".wav")&&paths[1]==Path.ChangeExtension(pb,".wav"),"Identical base names save beside their own sources");
  Check(!Directory.GetFiles(root,"*.csv",SearchOption.AllDirectories).Any(),"No queue summary or auxiliary CSV is created");
  jobs=new List<BatchJob>{BatchJob.Load(pa),BatchJob.Load(pb)};int call=0;
  BatchRunner.Run(jobs,false,()=>false,(j,p,n)=>{if(call++==0)throw new Exception("synthetic overrun");return p;},(p,m)=>{});
  Check(jobs[0].State==JobState.Failed&&jobs[1].State==JobState.Done&&jobs[0].Error.Contains("overrun"),"Synthesis failure is isolated and next file continues");
  Check(jobs[0].OutputPath==""&&jobs[0].Error.Contains("overrun"),"Failed job reports error in UI data without publishing a result");
  jobs=new List<BatchJob>{BatchJob.Load(pa),BatchJob.Load(pb)};bool stop=false;
  dest=BatchRunner.Run(jobs,false,()=>stop,(j,p,n)=>{stop=true;return p;},(p,m)=>{});
  Check(jobs[0].State==JobState.Done&&jobs[1].State==JobState.Queued,"Cancellation between jobs preserves remaining pending jobs");
  Check(jobs[1].OutputPath=="","Pending job has no output path after cancellation");
  jobs=new List<BatchJob>{BatchJob.Load(pa),BatchJob.Load(pb)};
  BatchRunner.Run(jobs,false,()=>false,(j,p,n)=>{throw new OperationCanceledException();},(p,m)=>{});
  Check(jobs[0].State==JobState.Cancelled&&jobs[1].State==JobState.Queued,"Mid-file cancellation stops current and does not start next");
  jobs[0].State=JobState.Done;call=0;BatchRunner.Run(jobs,true,()=>false,(j,p,n)=>{call++;return p;},(p,m)=>{});
  Check(call==1&&jobs.All(j=>j.State==JobState.Done),"Resume skips completed files and runs pending files only");
  string profiles=Path.Combine(root,"profiles.tsv"),notes=Path.Combine(root,"notes.tsv");File.WriteAllText(profiles,"Key\tGender\tDescription\tSource\nK:3\tNữ\tTông vừa; nhịp chậm\tMeasured sample\n",Encoding.UTF8);
  var catalog=new VoiceCatalog(profiles,notes);catalog.SaveNote("K:3","Hợp giọng truyện\nNgắt nhẹ.");catalog=new VoiceCatalog(profiles,notes);
  Check(catalog.Count==1&&catalog.Get("K:3").Description.Contains("chậm")&&catalog.Note("K:3")=="Hợp giọng truyện\nNgắt nhẹ.","Voice descriptions load and personal Unicode notes survive restart");
  Check(OutputFiles.Beside(Path.Combine(root,"kịch bản (1).srt"))==Path.Combine(root,"kịch bản (1).wav"),"Unicode source name and original stem are preserved");
  if(args.Length>0){
   var engine=new KokoroEngine{Root=Path.GetFullPath(args[0]),Threads=2};jobs=new List<BatchJob>{BatchJob.Load(pa),BatchJob.Load(pc),BatchJob.Load(pb)};
   BatchRunner.Run(jobs,false,()=>false,(j,p,n)=>Renderer.Render(j.Cues,engine,new RenderOptions{Locked=j.Timed,Breathing=BreathMode.Auto},p,n),(p,m)=>{});
   Check(jobs[0].State==JobState.Done&&jobs[2].State==JobState.Done,"Real offline batch generates both locked SRT and flexible TXT");
   Check(File.Exists(jobs[0].OutputPath)&&File.Exists(jobs[2].OutputPath),"Real batch writes separate complete WAV outputs");
   Check(File.ReadAllText(pa)==srt,"Batch leaves original SRT bytes unchanged");
   Check(Directory.GetFiles(Path.GetDirectoryName(pa)).Length==2&&Directory.GetFiles(Path.GetDirectoryName(pb)).Length==2&&Directory.GetDirectories(root).Length==2,"Batch source folders contain only original script and one WAV");
  }
  if(args.Length>1){var c=new VoiceCatalog(args[1],Path.Combine(root,"empty-notes.tsv"));Check(c.Count==53,"All 53 curated voice profiles installed");Check(c.All.Count(p=>p.Kind==EngineKind.LibriTTS)==20&&c.All.Where(p=>p.Kind==EngineKind.LibriTTS).All(p=>!String.IsNullOrWhiteSpace(p.Styles)),"Exactly 20 LibriTTS choices have descriptions and style tags");Check(c.Get("K:3").Recommended&&c.Get("K:2").Recommended&&c.Get("K:6").Recommended&&!c.Get("K:4").Recommended&&c.All.Count(p=>p.Kind==EngineKind.LibriTTS&&p.Recommended)==6,"Publisher and V15 recommendation sources remain distinguishable");}
  Console.WriteLine("TOTAL BATCH: "+count+" passed.");
 }
}
