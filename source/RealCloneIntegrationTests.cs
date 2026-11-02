using System;using System.IO;using System.Linq;using System.Collections.Generic;using V15Voice;
public static class RealCloneIntegrationTests {
 public static void Main(string[] args){string root=Path.GetFullPath(args[0]),output=Path.Combine(root,"real_clone_locked.wav");var cues=new List<Cue>{new Cue{Id=1,Start=0,End=12,Text="Sometimes the smallest detail changes the whole story."}};string before=Scripts.ToSrt(cues);
  using(var e=new KokoroEngine{Root=root,Kind=EngineKind.Qwen,ModelFolder="qwen_base",VoiceName="ADAM",ReferencePath=Path.Combine(root,"ai","reference.wav"),ReferenceText="",DeviceMode="cpu",Threads=4,ActivityChanged=s=>Console.WriteLine("ACTIVITY "+s)}){
   string final=Renderer.Render(cues,e,new RenderOptions{Breathing=BreathMode.None,SlowShort=false,MaxSpeed=1.3},output,(n,s)=>Console.WriteLine("RENDER "+n+" "+s));var w=Wave.Read(final);
   if(w.Rate!=24000||Math.Abs(w.Duration-12)>1.0/24000||Scripts.ToSrt(cues)!=before||cues[0].Actual<=0||!w.Samples.Any(x=>x!=0)||!e.DeviceInfo.StartsWith("CPU"))throw new Exception("Real cloned WAV or locked timeline validation failed");
   Console.WriteLine("PASS real Qwen 0.6B x-vector clone via production C# process bridge, PCM mono 24k, exact 12s locked WAV, unchanged SRT, voiced audio "+cues[0].Actual.ToString("0.000")+"s, "+e.DeviceInfo);Console.WriteLine("OUTPUT "+final);
   var tight=new List<Cue>{new Cue{Id=1,Start=0,End=3.3,Text=cues[0].Text}};string original=Scripts.ToSrt(tight);final=Renderer.Render(tight,e,new RenderOptions{Breathing=BreathMode.None,SlowShort=false,MaxSpeed=1.3},Path.Combine(root,"real_clone_tight.wav"),(n,s)=>Console.WriteLine("RENDER "+n+" "+s));w=Wave.Read(final);
   if(Math.Abs(w.Duration-3.3)>1.0/24000||tight[0].Actual>3.3||tight[0].Speed>1.3||Scripts.ToSrt(tight)!=original)throw new Exception("Real voice tight timeline fitting failed");
   Console.WriteLine("PASS same accepted clone fits a locked 3.300s cue using bounded tempo adjustment; speed "+tight[0].Speed.ToString("0.000")+"; SRT text/timecodes unchanged");Console.WriteLine("TIGHT_OUTPUT "+final);
  }
 }
}
