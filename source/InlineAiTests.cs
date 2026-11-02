using System;using System.IO;using System.Linq;using System.Collections.Generic;using V15Voice;
public static class InlineAiTests {
 static int count;static string dir;
 static void Check(bool v,string name){if(!v)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 sealed class E:KokoroEngine {public static List<string> Reads=new List<string>();public bool Fail;public override void CheckFiles(){if(Fail)throw new Exception("missing voice model");}public override Wave Generate(string text,double speed,string output){Reads.Add(text);int n=(int)(System.Text.RegularExpressions.Regex.Matches(text,@"\b\w+\b").Count*.30/speed*24000);return new Wave{Rate=24000,Samples=Enumerable.Range(0,n).Select(i=>i<n-1200?(short)(5000*Math.Sin(i*.06)):(short)0).ToArray()};}}
 sealed class W:ITextRewriter {public int Calls;public string Reply;public string Rewrite(string p,Func<bool> cancel,Action<string> a){if(cancel())throw new OperationCanceledException();Calls++;return Reply;}}
 static string Reply(params AiCueText[] cues){return LocalAi.Json(new AiRewriteText{cues=cues.ToList()});}
 static VoiceSettings Settings(){return new VoiceSettings{Mode=0,Key="K:3",Breathing=2,Slow=false,Normalize=false,GentleFit=true};}
 static Func<string,KokoroEngine> Factory=role=>new E{Root=dir,VoiceName="Fixture "+role,Archive=new SpeechArchive(Path.Combine(dir,"cache"))};
 static List<Cue> Script(){return Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:03,300\nJohn was standing there for a very long time because the door had finally opened.\n\n2\n00:00:03,450 --> 00:00:06,750\nHe was waiting there for a very long time because the door had finally opened.\n\n3\n00:00:06,900 --> 00:00:10,200\nThis small detail changes the whole story for everyone.\n");}
 static AiRepairSession Open(string name,List<Cue> cues,VoiceSettings s=null){string file=Path.Combine(dir,name+".srt");File.WriteAllText(file,Scripts.ToSrt(cues));return AiRepairSession.Open(file,cues,dir,s??Settings(),false);}
 public static int Main(){try{dir=Path.Combine(Path.GetTempPath(),"V15 inline "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var cues=Script();cues[0].Status="QUÁ DÀI: +1s";cues[1].Status="Dư 1s";cues[2].Status="Đạt";cues[2].Natural=2.8;
  Check(InlineAiRepair.TimingProblem(cues[0])&&InlineAiRepair.TimingProblem(cues[1])&&!InlineAiRepair.TimingProblem(cues[2]),"Only timing failures expose a repair action");
  Check(!InlineAiRepair.TimingProblem(new Cue{Status="Lỗi CUDA missing model"}),"GPU/model failure is not presented as a wording error");
  var p=Open("target",cues);string originalHash=StateFiles.Hash(p.Source);var w=new W{Reply=Reply(new AiCueText{id=1,text="John could not believe the door had finally opened."})};
  AiTimeline.Run(p,w,Factory,()=>false,(n,m)=>{},true,true,1);
  Check(w.Calls==1&&p.Rows[0].Chosen!=null,"One click measures, rewrites, remeasures and accepts the selected failing cue");
  Check(p.Rows[1].Original==null&&p.Rows[1].Attempts.Count==0&&p.Rows[2].Original==null,"Other failing and passing cues are not measured or rewritten");
  InlineAiRepair.Apply(p,cues,1);Check(cues[0].Text.Contains("could not believe")&&cues[0].Status.StartsWith("AI đã sửa"),"Measured accepted wording is applied into the existing row");
  Check(cues[1].Status=="Dư 1s"&&cues[2].Natural==2.8&&cues[2].Status=="Đạt","Other rows retain their wording, error status and measurement");
  Check(InlineAiRepair.Matches(p,cues)&&StateFiles.Hash(p.Source)==originalHash,"Effective script matches session and original SRT bytes remain untouched");
  var second=new W{Reply=Reply(new AiCueText{id=2,text="He could not believe the door had finally opened."})};AiTimeline.Run(p,second,Factory,()=>false,(n,m)=>{},true,true,2);InlineAiRepair.Apply(p,cues,2);
  Check(p.Rows[0].Chosen!=null&&p.Rows[1].Chosen!=null&&cues[0].Text.Contains("could not believe")&&cues[1].Text.Contains("could not believe"),"Repairing a second row preserves the first accepted repair");
  Check(File.ReadAllText(p.Report).Contains("Sửa #1")&&File.ReadAllText(p.Report).Contains("Sửa #2"),"Report retains history of both targeted repairs");
  var original=Script();p.Rows[0].Accepted=0;InlineAiRepair.Apply(p,cues,1);
  Check(cues[0].Text==original[0].Text&&cues[0].Status.Contains("Dư ")&&cues[1].Text.Contains("could not believe"),"Undo restores only the selected original row and its timing error");
  p.Rows[0].Accepted=p.Rows[0].Best.Number;InlineAiRepair.Apply(p,cues,1);
  var options=AudioRepair.Options(p.Settings);options.NormalizeLoudness=false;string output=Path.Combine(dir,"final.wav");using(var e=Factory("A"))Renderer.Render(cues,e,options,output,(n,m)=>{});
  Check(FinalAudio.PcmFrames(output)==244800,"Repaired script exports a full WAV exactly 10.2 seconds long");
  var invalid=Open("invalid",Script());var bad=new W{Reply="not JSON"};AiTimeline.Run(invalid,bad,Factory,()=>false,(n,m)=>{},true,true,1);
  Check(bad.Calls==2&&invalid.Rows[0].Chosen==null&&invalid.Rows[1].Original==null,"Invalid proposals stop after two attempts without touching other rows");
  var keep=Script();InlineAiRepair.Apply(invalid,keep,1);Check(keep[0].Text==original[0].Text,"Unmeasured or invalid AI output never replaces source wording");
  var failing=Open("model-error",Script());var never=new W{Reply="{}"};AiTimeline.Run(failing,never,role=>new E{Fail=true},()=>false,(n,m)=>{},true,true,2);
  Check(never.Calls==0&&failing.Rows[1].Original.Error.Contains("missing voice model")&&failing.Rows[0].Original==null,"Selected model error is reported without an AI rewrite");
  var dual=Script();dual[1].Speaker="B";var settings=Settings();settings.TwoVoices=true;settings.VoiceBKey="private:B";var two=Open("dual",dual,settings);AiTimeline.Run(two,second,Factory,()=>false,(n,m)=>{},true,true,2);InlineAiRepair.Apply(two,dual,2);
  Check(two.Rows[1].Original.Voice=="Fixture B"&&dual[1].Speaker=="B","Selected B cue uses B voice and preserves its role");
  var stage=Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:02,000\nJohn was standing there for a very long time\n\n2\n00:00:02,100 --> 00:00:04,000\nbecause the door had finally opened for everyone.\n\n3\n00:00:04,100 --> 00:00:07,400\nThis small detail changes the whole story for everyone.\n");stage[1].AnchorAfter=true;settings=Settings();settings.Mode=2;var anchored=Open("stage",stage,settings);
  var rewrite=new W{Reply=Reply(new AiCueText{id=1,text="John waited there for a long time"},new AiCueText{id=2,text="until the door opened for everyone."})};AiTimeline.Run(anchored,rewrite,Factory,()=>false,(n,m)=>{},true,true,2);
  Check(anchored.Rows[0].Chosen!=null&&anchored.Rows[1].Original==null,"Clicking an interior cue repairs exactly its V15.6 stage");
  InlineAiRepair.Apply(anchored,stage,2);Check(stage[0].Text=="John waited there for a long time"&&stage[1].Text.Contains("until the door")&&stage[1].AnchorAfter&&stage[2].Text.StartsWith("This small"),"Stage repair updates its member cues while preserving anchors and other stages");
  var snapshot=AiRepairSession.SnapshotHash(stage);bool rejected=false;try{AiTimeline.Run(anchored,rewrite,Factory,()=>true,(n,m)=>{},true,true,3);}catch(OperationCanceledException){rejected=true;}Check(rejected&&AiRepairSession.SnapshotHash(stage)==snapshot,"Cancellation leaves the active script unchanged");
  File.AppendAllText(anchored.Source,"\nchanged");rejected=false;try{InlineAiRepair.Apply(anchored,stage,2);}catch{rejected=true;}Check(rejected,"Externally changed source blocks applying stale repair");
  Console.WriteLine("TOTAL INLINE AI "+count+" passed. Fixture TTS/rewriter, not a real model/GPU test.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
