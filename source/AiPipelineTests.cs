using System;using System.IO;using System.Linq;using System.Threading;using System.Threading.Tasks;using V15Voice;
public static class AiPipelineTests {
 static int count;static void Check(bool v,string s){if(!v)throw new Exception("FAIL "+s);Console.WriteLine("PASS "+s);count++;}
 public static void Main(string[] args){string root=args[0],dir=Path.Combine(Path.GetTempPath(),"V15_ai_tests_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  foreach(var kind in new[]{EngineKind.Qwen,EngineKind.Chatterbox})using(var e=new KokoroEngine{Root=root,Kind=kind,VoiceName="Aiden",ModelFolder=kind==EngineKind.Qwen?"qwen_custom":"chatter_nano",Threads=4}){
   var cues=Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:04,000\nThe red light appeared every night.\n\n2\n00:00:04,150 --> 00:00:08,150\nThen Sarah noticed one unusual detail.\n");string srt=Scripts.ToSrt(cues);
   string dest=Renderer.Render(cues,e,new RenderOptions{Locked=true,Breathing=BreathMode.None,SlowShort=false},Path.Combine(dir,kind+".wav"),(p,m)=>Console.WriteLine(kind+" "+p+" "+m));Wave w=Wave.Read(dest);
   Check(Math.Abs(w.Duration-8.15)<1.0/w.Rate,kind+" locked WAV retains exact master duration");Check(Scripts.ToSrt(cues)==srt,kind+" rendering preserves every source word and timestamp");Check(w.Samples.Skip(4*w.Rate).Take((int)(.15*w.Rate)).All(x=>x==0),kind+" locked gap contains no speech overlap");Check(cues.All(c=>c.Actual<=c.Duration),kind+" measured cue durations fit their frames");
   Wave normal=e.Generate("A quiet room can hide a strange story.",1,Path.Combine(dir,"normal.wav")),fast=e.Generate("A quiet room can hide a strange story.",1.3,Path.Combine(dir,"fast.wav"));Check(fast.Duration<normal.Duration*.85,kind+" cached synthesis changes duration through pitch-preserving tempo");
  }
  int before=Directory.GetDirectories(Path.GetTempPath(),"V15_ai_owner_*").Length;using(var cancelled=new KokoroEngine{Root=root,Kind=EngineKind.Qwen,ModelFolder="qwen_custom",VoiceName="Aiden",Threads=2}){
   var task=Task.Run(()=>{try{cancelled.Generate("This sentence is cancelled while the model starts.",1,Path.Combine(dir,"cancel.wav"));return false;}catch(OperationCanceledException){return true;}});Thread.Sleep(600);cancelled.Cancel();Check(task.Wait(20000)&&task.Result,"Cancellation interrupts AI model startup without waiting for synthesis");
  }Check(Directory.GetDirectories(Path.GetTempPath(),"V15_ai_owner_*").Length==before,"AI cancellation cleans owned temporary speech directory");Check(!Directory.GetFiles(dir,"*.csv").Any(),"AI pipeline exports WAV only");
  Console.WriteLine("TOTAL "+count+" passed");
 }
}
