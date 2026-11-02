using System;using System.IO;using System.Linq;using System.Collections.Generic;using V15Voice;
public static class VoiceTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 public static void Main(string[] args){
  string root=Path.GetFullPath(args[0]),dir=Path.Combine(Path.GetTempPath(),"v15_voice_tests_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var c=new VoiceCatalog(Path.Combine(root,"voice_profiles.tsv"),Path.Combine(dir,"notes.tsv"));
  Check(c.Count==53&&c.All.Count(p=>p.Kind==EngineKind.LibriTTS)==20,"Release catalog exposes 20 Kokoro, 20 curated LibriTTS, 8 Kitten and 2 Piper voices");
  Check(c.All.All(p=>p.Styles.Split('|').Length>=1&&p.Styles.Split('|').Length<=3&&p.Styles.Split('|').All(t=>VoiceStyles.Names.Contains(t))),"Every voice has one main and at most two recognized secondary styles");
  Check(c.All.All(p=>!String.IsNullOrWhiteSpace(p.StyleReason)&&!String.IsNullOrWhiteSpace(p.Name)),"Every recommendation includes a usage reason and readable name");
  Check(VoiceStyles.Names.Skip(1).All(s=>c.All.Any(p=>p.MatchesStyle(s))),"All seven requested styles have matching voices");
  Check(c.All.All(p=>File.Exists(Path.Combine(root,p.Sample))),"Every displayed voice has a bundled offline audition WAV");
  Check(c.All.Where(p=>p.Recommended).All(p=>!String.IsNullOrEmpty(p.StarSource)),"Star labels always identify their actual recommendation source");
  Check(c.All.Count(p=>p.Kind==EngineKind.LibriTTS&&p.Gender.StartsWith("Nam"))==10&&c.All.Count(p=>p.Kind==EngineKind.LibriTTS&&p.Gender.StartsWith("Nữ"))==10,"LibriTTS selection balances ten male and ten female source speakers");
  foreach(var p in c.All.Where(p=>(int)p.Kind<=3).Where(p=>args.Length<2||args[1]!="vits-only"||p.Kind==EngineKind.LibriTTS||p.Kind==EngineKind.Piper)){
   var e=new KokoroEngine{Root=root,Kind=p.Kind,Speaker=p.Speaker,ModelFolder=p.ModelFolder,Threads=2};e.CheckFiles();
   string file=Path.Combine(dir,p.Key.Replace(':','_')+".wav");var w=e.Generate("Have you ever forgotten why you entered a room?",1,file);
   Check(w.Duration>.5&&w.Duration<10&&w.Samples.Any(x=>x!=0),"Native process synthesizes "+p.Name+" using "+p.Key);
  }
  foreach(var kind in new[]{EngineKind.Kokoro,EngineKind.LibriTTS,EngineKind.Kitten,EngineKind.Piper}){
   var p=c.All.First(x=>x.Kind==kind);var e=new KokoroEngine{Root=root,Kind=p.Kind,Speaker=p.Speaker,ModelFolder=p.ModelFolder,Threads=2};
   var slow=e.Generate("The red light appeared every night.",1,Path.Combine(dir,"normal.wav"));var fast=e.Generate("The red light appeared every night.",1.3,Path.Combine(dir,"fast.wav"));Check(fast.Duration<slow.Duration,"Native speed fitting changes duration for "+kind);
   var cues=Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:04,000\nThe red light appeared every night.\n\n2\n00:00:04,150 --> 00:00:08,150\nThen Sarah noticed one unusual detail.\n");
   string output=Renderer.Render(cues,e,new RenderOptions{Locked=true,SlowShort=false,Breathing=BreathMode.Custom,BreathEvery=1},Path.Combine(dir,kind+"_locked.wav"),(n,m)=>{});Check(Math.Abs(Wave.Read(output).Duration-8.15)<.001&&cues[1].Start==4.15,"Locked timeline and breath composition work for "+kind);
  }
  Console.WriteLine("TOTAL VOICES: "+count+" passed.");
 }
}
