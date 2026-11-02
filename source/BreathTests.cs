using System;using System.IO;using System.Linq;using System.Collections.Generic;using V15Voice;
public static class BreathTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 static void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}Check(false,name);}
 static double Rms(short[] x){return Math.Sqrt(x.Average(y=>(double)y*y));}
 public static void Main(string[] args){
  string root=Path.GetFullPath(args[0]),dir=Path.Combine(Path.GetTempPath(),"v15_breath_tests_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var options=new RenderOptions();Check(options.Breathing==BreathMode.Auto,"Auto is the default breath mode");
  var track=new BreathTrack(options,root);Wave breath=null;
  Check(track.After(new Cue{Text="A clause without a sentence ending",End=3},24000)==null,"Subtitle boundaries inside a sentence do not trigger a breath");
  for(int i=0;i<3;i++){breath=track.After(new Cue{Text="This sentence ends here.",End=3+i*3},24000);if(i<2)Check(breath==null,"Auto waits for three sentence boundaries "+i);}
  Check(breath!=null&&breath.Duration<.4,"Auto selects bundled short real breath");track.Inserted(new Cue{End=9});
  for(int i=0;i<3;i++)breath=track.After(new Cue{Text="Another sentence.",End=10+i},24000);
  Check(breath==null,"Auto minimum spacing prevents dense breath repetition");
  var custom=new RenderOptions{Breathing=BreathMode.Custom,BreathEvery=1,BreathLevel=.16};track=new BreathTrack(custom,root);
  Check(track.After(new Cue{Text="Hello.",End=1},24000,.05)==null,"A whole breath is skipped when fixed timeline has insufficient room");
  breath=track.After(new Cue{Text="Hello.",End=2},22050,1);Check(breath.Rate==22050,"Bundled breath is resampled for LibriTTS");
  var speech=new Wave{Rate=22050,Samples=Enumerable.Range(0,22050).Select(i=>(short)(5000*Math.Sin(i*.05))).ToArray()};var master=new short[breath.Samples.Length];track.Mix(breath,speech,master,0);
  Check(Math.Abs(Rms(master)/Rms(speech.Samples)-.16)<.015,"Breath level follows selected speech RMS rather than raw sample volume");Check(master[0]==0&&master.Last()==0,"Breath edges fade without boundary clicks");
  var none=new BreathTrack(new RenderOptions{Breathing=BreathMode.None,BreathPath="missing.wav"},"missing-model-root");Check(none.After(new Cue{Text="Hello.",End=3},24000)==null,"Off mode never loads or inserts breath audio");
  string longSample=Path.Combine(dir,"long.wav");new Wave{Rate=24000,Samples=Enumerable.Repeat((short)1000,24000*3).ToArray()}.Write(longSample);Reject(()=>new BreathTrack(new RenderOptions{Breathing=BreathMode.Custom,BreathPath=longSample},root),"Reject custom breath longer than two seconds");
  string requested=Path.Combine(dir,"kịch bản.wav");File.WriteAllText(requested,"existing user WAV");string pending=Path.Combine(dir,"pending.tmp");File.WriteAllText(pending,"new");string published=OutputFiles.Publish(pending,requested);Check(published==Path.Combine(dir,"kịch bản (2).wav")&&File.ReadAllText(requested)=="existing user WAV","Collision adds suffix and preserves the existing WAV bytes");
  string srt="1\n00:00:00,000 --> 00:00:03,000\nThis is the first sentence.\n\n2\n00:00:04,000 --> 00:00:07,000\nThis is the second sentence.\n\n3\n00:00:08,000 --> 00:00:11,000\nThis is the third sentence.\n\n4\n00:00:12,000 --> 00:00:15,000\nThis is the final sentence.\n";
  var engine=new KokoroEngine{Root=root,Threads=2};var cues=Scripts.ParseSrt(srt);var fixedOptions=new RenderOptions{Locked=true,SlowShort=false};
  string outputDir=Path.Combine(dir,"results");Directory.CreateDirectory(outputDir);string output=Renderer.Render(cues,engine,fixedOptions,Path.Combine(outputDir,"test.wav"),(p,m)=>{});Wave result=Wave.Read(output);
  Check(Math.Abs(result.Duration-15)<1.0/result.Rate&&Scripts.ToSrt(cues)==Scripts.ToSrt(Scripts.ParseSrt(srt)),"Auto breath preserves every locked SRT timestamp and final duration");
  Check(cues.Count(c=>c.Status.Contains("hơi thở"))==1&&cues[2].Status.Contains("hơi thở"),"Auto inserts only after the third complete sentence");int begin=(int)Math.Round((cues[2].Start+cues[2].Actual+.04)*result.Rate);Check(result.Samples.Skip(begin).Take((int)(.18*result.Rate)).Any(x=>x!=0),"Real offline final WAV contains breath audio in the available gap");
  Check(Directory.GetFiles(outputDir).Length==1&&Directory.GetDirectories(outputDir).Length==0,"Successful render exports exactly one WAV without reports or cue folders");
  var flex=Scripts.ParseText("This is one sentence. This is another sentence. Here comes the third sentence. This is the last sentence.");string f=Renderer.Render(flex,engine,new RenderOptions{Locked=false,Breathing=BreathMode.Custom,BreathEvery=1},Path.Combine(outputDir,"flex.wav"),(p,m)=>{});
  Check(flex.Take(3).All(c=>c.Status.Contains("hơi thở"))&&!flex.Last().Status.Contains("hơi thở"),"Custom frequency works; no trailing breath after the last cue");Check(flex[1].Start-flex[0].End>.25&&Math.Abs(Wave.Read(f).Duration-flex.Last().End)<.001,"Flexible timeline reserves complete breath and silence without clipping");
  int before=Directory.GetFiles(outputDir).Length;string[] workBefore=Directory.GetDirectories(Path.GetTempPath(),"V15_work_*");
  Reject(()=>Renderer.Render(Scripts.ParseText("This will be cancelled."),engine,new RenderOptions{Locked=false},Path.Combine(outputDir,"cancel.wav"),(p,m)=>{if(p==95)engine.Cancelled=true;}),"Cancellation during finalization rejects publication");
  Check(Directory.GetFiles(outputDir).Length==before&&!Directory.GetDirectories(Path.GetTempPath(),"V15_work_*").Except(workBefore).Any(),"Cancellation leaves no output, partial file, or temporary cue directory");engine.Cancelled=false;
  var impossible=Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:00,080\nThis sentence cannot fit in this very short time window.\n");Reject(()=>Renderer.Render(impossible,engine,new RenderOptions(),Path.Combine(outputDir,"failure.wav"),(p,m)=>{}),"Overrun never publishes a misleading completed WAV");Check(Directory.GetFiles(outputDir).Length==before,"Overrun leaves only earlier successful WAV files");
  Console.WriteLine("TOTAL BREATH/OUTPUT: "+count+" passed.");
 }
}
