using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Xml.Linq;using V15Voice;
public sealed class PacingSpeech:KokoroEngine {
 public List<string> Texts=new List<string>();public double Seconds=.4,Edge;
 public override void CheckFiles(){}
 public override Wave Generate(string text,double speed,string output){Texts.Add(text);int n=(int)(Seconds*24000),edge=(int)(Edge*24000);var data=new short[n+2*edge];for(int i=0;i<n;i++)data[i+edge]=(short)(5000*Math.Cos(i*.0576));return new Wave{Rate=24000,Samples=data};}
}
public static class PunctuationTests {
 static int count;static void Check(bool x,string name){if(!x)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 static void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}throw new Exception("FAIL "+name);}
 public static void Main(){var o=new RenderOptions{Punctuation=PunctuationMode.Custom,Breathing=BreathMode.None,SlowShort=false};
  var plan=Punctuation.Split("First, second. Third? Last!",o);Check(plan.Count==4&&plan[0].Pause==.14&&plan[1].Pause==.30&&plan[2].Pause==.35&&plan[3].Pause==0,"Comma, stop and question pauses; no trailing gap");
  Check(Punctuation.Split("The price is 1,200.50 dollars.",o).Count==1,"Decimal and thousands separators stay within speech");
  Check(Punctuation.Split("Dr. Smith works in the U.S. office.",o).Count==1,"Titles and initialisms do not split words");
  Check(Punctuation.Split("Wait... What next?",o).Count==2,"Ellipsis is a single boundary");
  Check(Punctuation.Split("Done.\n\nNext paragraph.",o)[0].Pause==.5,"Paragraph pause replaces stop pause");
  Check(Punctuation.Split("Are you ready?\" Then go.",o).Count==2,"Closing quotes stay with their sentence");
  Check(VoiceSettings.From(new XElement("settings")).Punctuation==0,"Old settings keep natural Auto pacing");
  var saved=new VoiceSettings{Punctuation=1,CommaPause=.22,StopPause=.44,QuestionPause=.6,ParagraphPause=.8};var restored=VoiceSettings.From(saved.Xml());Check(restored.Punctuation==1&&restored.CommaPause==.22&&restored.StopPause==.44&&restored.QuestionPause==.6&&restored.ParagraphPause==.8,"Custom punctuation values survive XML roundtrip");
  string root=Path.Combine(Path.GetTempPath(),"v15-pacing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var e=new PacingSpeech{Root=root};string output=Path.Combine(root,"cue.wav");
  Punctuation.Generate(e,"One, two. Three?",1,new RenderOptions(),output);Check(e.Texts.SequenceEqual(new[]{"One, two. Three?"}),"Auto passes full unmodified text to engine once");e.Texts.Clear();
  var speech=Punctuation.Generate(e,"One, two. Three?",1,o,output);Check(e.Texts.SequenceEqual(new[]{"One,","two.","Three?"}),"Custom pacing retains punctuation in every phrase");Check(Math.Abs(speech.Join().Duration-1.64)<.0001,"Custom adds only requested internal gaps");
  e.Edge=.05;speech=Punctuation.Generate(e,"One, two.",1,o,output);Check(Math.Abs(speech.PauseSeconds-.04)<.0001,"Native silence at both phrase edges is deducted from inserted gap");e.Edge=0;
  speech=Punctuation.Generate(e,"One, two.",1,o,output);double reserve=0;var cue=new Cue{Id=1,Start=0,End=.9,Text="One, two."};var fitted=Punctuation.Fit(speech,cue,o,root,root,()=>false,ref reserve);Check(fitted.Duration<=.9&&cue.Speed==1,"Shorten added pause before speeding speech");
  speech=Punctuation.Generate(e,"One.",1,o,output);reserve=.3;cue=new Cue{Id=1,Start=0,End=.5,Text="One."};fitted=Punctuation.Fit(speech,cue,o,root,root,()=>false,ref reserve);Check(reserve<.3&&cue.Speed==1&&fitted.Duration+reserve<=.5,"Shorten reserved terminal pause before speeding speech");
  var cues=new List<Cue>{new Cue{Id=1,Start=0,End=2,Text="One."},new Cue{Id=2,Start=2,End=4,Text="Two."}};o.Locked=false;o.Gap=.15;string result=Renderer.Render(cues,e,o,Path.Combine(root,"flex.wav"),(n,m)=>{});Check(Math.Abs(cues[1].Start-cues[0].End-.3)<.0001,"Flexible timeline uses maximum gap instead of summing pause twice");
  cues=new List<Cue>{new Cue{Id=1,Start=0,End=1,Text="One, two."},new Cue{Id=2,Start=1.1,End=2,Text="Three."}};string original=Scripts.ToSrt(cues);o.Locked=true;result=Renderer.Render(cues,e,o,Path.Combine(root,"locked.wav"),(n,m)=>{});Check(Scripts.ToSrt(cues)==original&&Wave.Read(result).Duration==2,"Locked render preserves exact timecodes and final WAV duration");
  e.Seconds=2;Reject(()=>Renderer.Render(new List<Cue>{new Cue{Id=1,Start=0,End=.1,Text="Impossible."}},e,o,Path.Combine(root,"bad.wav"),(n,m)=>{}),"Unfixable cue rejects render");Check(!File.Exists(Path.Combine(root,"bad.wav")),"Failed render publishes no invalid WAV");
  o.CommaPause=Double.NaN;Reject(()=>Punctuation.Validate(o),"Reject NaN pause");o.CommaPause=1.51;Reject(()=>Punctuation.Validate(o),"Reject pause beyond UI maximum");
  Directory.Delete(root,true);Console.WriteLine("TOTAL PUNCTUATION "+count+" passed.");
 }
}
