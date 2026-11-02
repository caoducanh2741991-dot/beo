using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Reflection;using System.Collections.Generic;using V15Voice;
public static class Lite412Tests {
 static int n;static void Check(bool yes,string why){if(!yes)throw new Exception(why);n++;Console.WriteLine("PASS "+why);}
 static void Removed(Action action,string why){try{action();throw new Exception("Removed feature executed: "+why);}catch(NotSupportedException){Check(true,why);}}
 public static int Main(){try{
  Check(typeof(KokoroEngine).Assembly.GetName().Version.ToString()=="4.1.2.0","Lite binary has its own 4.1.2 version");
  string root=RenderPipelineFixture.Root(),source=RenderPipelineFixture.Source(root,4),originalHash=StateFiles.Hash(source);var original=BatchJob.Load(source).Cues;
  int prepareCalls=0;var translator=new RenderPipelineFixture.NeverRewrite();var o=RenderPipelineFixture.Options(root,source);o.Translator=translator;
  WorkflowResult first;using(var engine=RenderPipelineFixture.Engine(root,Path.Combine(root,"speech-cache")))first=WorkflowRunner.Run(original,engine,o,OutputFiles.Beside(source),2,(words,options,path,owner,progress)=>{prepareCalls++;foreach(var cue in words)cue.Text="Invented rewrite";return words;},(p,m)=>{});
  Check(first.Status=="FINAL_VERIFIED"&&File.Exists(first.Bundle.Wave),"A restored mode-2 request completes as a real source-only render");
  Check(prepareCalls==0&&translator.Calls==0&&o.Translator==null,"Neither rewrite nor translation callback executes");
  Check(first.Cues.Select(c=>c.Text).SequenceEqual(original.Select(c=>c.Text)),"Original words reach the voice pipeline unchanged");
  var final=Scripts.ParseSrt(File.ReadAllText(first.Bundle.Srt));Check(final.Select(c=>c.Text).SequenceEqual(original.Select(c=>c.Text)),"Final SRT words match the actual render input");
  Check(final.Select(c=>c.Start).SequenceEqual(original.Select(c=>c.Start))&&final.Select(c=>c.End).SequenceEqual(original.Select(c=>c.End)),"Locked source timecodes are unchanged");
  Check(StateFiles.Hash(source)==originalHash,"Input file bytes are preserved");
  Check(first.Bundle.Reports.Length==1&&first.Bundle.Primary.EndsWith(".voice-report.txt")&&first.Bundle.Changes=="","Lite publishes exactly one report per source");
  Check(File.ReadAllText(first.Bundle.Primary).Contains("BÉO VOICE STUDIO LITE 4.1.2")&&!Directory.GetFiles(root,"*.ai-*.txt").Any(),"Report identifies Lite and no AI report is produced");
  Check(RenderPipelineFixture.Lines(root,"requests")==4,"Speech engine still synthesizes all four original cues");
  string waveHash=StateFiles.Hash(first.Bundle.Wave);RenderOptions warmOptions;var warm=RenderPipelineFixture.Run(root,source,1,out warmOptions);
  Check(warm.Status=="FINAL_VERIFIED"&&StateFiles.Hash(warm.Bundle.Wave)==waveHash,"Warm checkpoint render produces the same WAV bytes");
  Check(RenderPipelineFixture.Lines(root,"requests")==4,"Validated locked PCM is reused without repeating TTS");
  var legacy=VoiceSettings.From(new XElement("settings",new XAttribute("workflowMode",2),new XAttribute("aiMode",2),new XAttribute("twoVoices",1),new XAttribute("voiceBKey","K:4")));
  Check(legacy.WorkflowMode==1&&legacy.AiMode==0&&legacy.TwoVoices&&legacy.VoiceBKey=="K:4","Legacy AI settings normalize safely while keeping two-voice settings");
  var job=BatchJob.Load(source);job.WorkflowMode=2;job.State=JobState.Done;job.OutputPath=first.Bundle.Wave;job.AiReport="old AI report";job.RunSettingsKey="old AI cache";job.Cues[0].Text="Old AI wording";job.Progress.Restore(100,120,true);string queue=Path.Combine(root,"legacy-queue.xml");QueueStore.Save(queue,new[]{job});var restored=QueueStore.Load(queue).Single();
  Check(restored.State==JobState.Queued&&restored.WorkflowMode==1&&restored.Progress.Read().Percent==0,"Old AI jobs are queued as a fresh source-only task");
  Check(restored.Cues.Select(c=>c.Text).SequenceEqual(original.Select(c=>c.Text))&&restored.AiReport==""&&restored.RunSettingsKey=="","Old rewritten words and AI cache identity cannot leak into Lite");
  job=BatchJob.Load(source);job.WorkflowMode=1;job.State=JobState.Done;job.OutputPath=first.Bundle.Wave;job.RunSettingsKey="valid source render";job.Progress.Restore(100,20,true);QueueStore.Save(queue,new[]{job});restored=QueueStore.Load(queue).Single();
  Check(restored.State==JobState.Done&&restored.RunSettingsKey==job.RunSettingsKey,"Completed source-only jobs remain valid after restart");
  string bad=Path.Combine(root,"bad.srt");File.WriteAllText(bad,"not a subtitle");var badOptions=RenderPipelineFixture.Options(root,bad);WorkflowResult failure;using(var engine=RenderPipelineFixture.Engine(root))failure=WorkflowRunner.Run(new List<Cue>(),engine,badOptions,OutputFiles.Beside(bad),2,(w,a,b,c,d)=>{prepareCalls++;return w;},(p,m)=>{});
  Check(failure.Status=="INPUT_ERROR"&&!failure.Rendered&&failure.Bundle.Reports.Length==1&&File.Exists(failure.Bundle.Primary),"Invalid source ends with one truthful report and no WAV");
  string broken=Path.Combine(root,"missing-engine");var engineOptions=RenderPipelineFixture.Options(root,source);engineOptions.CheckpointRoot=Path.Combine(root,"different-pcm");using(var engine=RenderPipelineFixture.Engine(broken))failure=WorkflowRunner.Run(original,engine,engineOptions,Path.Combine(root,"engine-error.wav"),1,null,(p,m)=>{});
  Check(failure.Status=="ENGINE_ERROR"&&!failure.Rendered&&failure.Bundle.Reports.Length==1,"Missing speech model is reported as an engine failure without AI attempts");
  string absent=Path.Combine(root,"no-llm-folder");Check(!LocalAi.Ready(absent),"Lite never advertises a language model as installed");
  Removed(()=>LocalAi.Setup(absent,()=>false,m=>{}),"Language-model setup is unavailable");
  Removed(()=>LocalAi.Download("https://invalid.example/unused",Path.Combine(absent,"model"),1,"",()=>false,m=>{}),"Language-model downloading is unavailable");
  Removed(()=>new LocalAi(absent,"gpu").Rewrite("ignored",()=>false,m=>{}),"Language-model inference is unavailable");
  Check(!Directory.Exists(absent)&&typeof(LocalAi).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Instance)==null,"Removed model operations cannot create files or launch a worker");
  Check(!RenderPipelineFixture.Alive(root),"TTS worker is released after rendering");
  Console.WriteLine("TOTAL LITE412 "+n+" PASS · real processes/FFmpeg, synthetic speech; no physical Windows/GPU/model inference");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}

