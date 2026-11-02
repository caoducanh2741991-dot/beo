using System;using System.IO;using System.Linq;using System.Collections.Generic;using V15Voice;
public static class Acoustic300Tests {
 static int count;static string root;static int calls;
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);count++;Console.WriteLine("PASS "+why);}
 sealed class Voice:KokoroEngine {public override void CheckFiles(){}public override Wave Generate(string text,double speed,string path){calls++;return new Wave{Rate=24000,Samples=Enumerable.Range(0,(int)Math.Round(3.325/speed*24000)).Select(i=>(short)(5500*Math.Cos(i*.061))).ToArray()};}}
 sealed class Ai:ITextRewriter {public int Calls;public string Rewrite(string p,Func<bool> c,Action<string> a){Calls++;throw new Exception("Unexpected model call for a pause-only correction");}}
 static Voice Engine(){return new Voice{Root=root,VoiceName="Acoustic fixture",DeviceMode="cpu"};}
 public static int Main(){try{
  root=Path.Combine(Path.GetTempPath(),"Acoustic300 "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var settings=new VoiceSettings{Mode=2,WorkflowMode=2,AiMode=2,Breathing=(int)BreathMode.None,Gap=.05,Punctuation=0,Normalize=false,Slow=false,GentleFit=false,MinSpeed=.9,MaxSpeed=1.25};
  var cues=new List<Cue>();string[] texts={"The worker checked the old wooden floor.","The next room needed some careful work.","We carried all the tools back upstairs.","Then the two of us walked outside."};for(int i=0;i<4;i++)cues.Add(new Cue{Id=i+1,Start=i*3.75,End=(i+1)*3.75,Text=texts[i]});
  double[] before=new double[2],after=new double[2];
  for(int mode=1;mode<=2;mode++){
   string dir=Path.Combine(root,"mode"+mode);Directory.CreateDirectory(dir);string source=Path.Combine(dir,"story.srt");File.WriteAllText(source,Scripts.ToSrt(cues));var o=AudioRepair.Options(settings.Xml().ToString());o.ReportContext=new VoiceReportContext{SourcePath=source};o.CheckpointRoot=Path.Combine(dir,"pcm");var ai=new Ai();o.Translator=ai;int startCalls=calls;WorkflowResult result;
   using(var e=Engine())result=WorkflowRunner.Run(cues,e,o,Path.Combine(dir,"out.wav"),mode,(words,options,path,owner,progress)=>{var session=AiRepairSession.Open(path,words,dir,settings,true,"acoustic300");options.AiRepair=session;AiTimeline.Run(session,ai,role=>Engine(),()=>owner.Cancelled,progress,true,true,0,true);return session.Effective();},(n,m)=>{});
   Check(result.Rendered&&result.Status=="FINAL_VERIFIED"&&result.Bundle.Reports.Length==mode,"Mode "+mode+" performs a measured, bounded pause repair with correct report count");
   Check(ai.Calls==0&&WorkflowReports.Changed(o,result.Cues).Count==0,"Mode "+mode+" pause correction never changes words or invokes rewrite/translation AI");
   Check(calls-startCalls==4,"Mode "+mode+" needs only four real fixture synthesis requests; the final assembly reuses measured PCM");
   var stage=o.RunReport.Stages.Single();before[mode-1]=stage.Before.Value;after[mode-1]=stage.After.Value;Check(Math.Abs(stage.After.Value-15)<1.0/24000&&stage.Before.Value-13.45<=.241&&stage.Speed>=.9,"Mode "+mode+" respects measured target, pause limit and technical minimum speed");
   if(mode==2){var m=o.AiRepair.Rows[0].Original;Check(m.PauseAdjustment>0&&m.PauseAdjustment<=.24&&Math.Abs(m.BeforePauseNatural-13.45)<.001&&m.BeforePauseAfter<15&&m.After==15,"Before/after report retains the actual pre-pause failure and the measured bounded-pause success");Check(File.ReadAllText(result.Bundle.Changes).Contains("ĐÃ SỬA: 0"),"A pause-only fix never masquerades as an AI wording change");}
  }
  Check(Math.Abs(before[0]-before[1])<1.0/24000&&Math.Abs(after[0]-after[1])<1.0/24000,"Measurement and final render share one acoustic repair policy in both modes");
  Console.WriteLine("TOTAL ACOUSTIC300 "+count+" PASS. Synthetic speech and actual FFmpeg, no word rewrite or native silence edit.");return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
}
