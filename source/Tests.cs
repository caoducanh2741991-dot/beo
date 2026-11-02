using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using V15Voice;
public static class Tests {
 static int count=0;
 static void Check(bool yes,string name){if(!yes)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS "+name);}
 static void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}Check(false,name);}
 public static void Main(string[] args){
  var s="1\r\n00:00:00,000 --> 00:00:03,300\r\nHave you ever forgotten why you entered a room?\r\n\r\n2\r\n00:00:03,450 --> 00:00:06,750\r\nYour brain may have switched contexts at the doorway.\r\n";
  var cues=Scripts.ParseSrt(s);Check(cues.Count==2&&Math.Abs(cues[1].Start-3.45)<1e-8,"SRT parse CRLF and 150ms gap");
  var roundtrip=Scripts.ParseSrt(Scripts.ToSrt(cues));Check(roundtrip[0].Text==cues[0].Text&&roundtrip[1].End==6.75,"SRT roundtrip words and exact milliseconds");
  Reject(()=>Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:03,000\nHello\n\n2\n00:00:02,000 --> 00:00:04,000\nThere"),"Reject overlapping cues");
  Reject(()=>Scripts.Time("00:69:00,000"),"Reject invalid minutes");
  Reject(()=>Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:03,000\nHello\n2\n00:00:03,000 --> 00:00:04,000\nThere"),"Reject missing separator instead of reading timestamps");
  var plain="This is a very long sentence that should be split into several shorter spoken units without losing a single original word, even if it needs more than one subtitle block.";
  var txt=Scripts.ParseText(plain);Check(String.Join(" ",txt.Select(x=>x.Text))==plain,"TXT splitting preserves every word");Check(txt.All(x=>x.Words<=10),"TXT V15 word budget");
  var timed=Scripts.ParseText("[00:00:00.000 - 00:00:03.300] Hello there, how are you doing today?");Check(timed.Count==1&&timed[0].End==3.3,"Timed TXT parse");
  Reject(()=>Scripts.ParseText("[00:00:00.000 - 00:00:03.300] Hello\nUntimed text"),"Reject mixed timed/untimed overlaps");
  Check(KokoroEngine.Q("a\"b")=="\"a\\\"b\"","Windows quote escaping");
  Check(KokoroEngine.Q("C:\\folder\\")=="\"C:\\folder\\\\\"","Windows trailing backslash quoting");
  string temp=Path.Combine(Path.GetTempPath(),"v15_tests_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  var w=new Wave{Rate=24000,Samples=Enumerable.Range(0,24000).Select(i=>(short)(5000*Math.Sin(2*Math.PI*220*i/24000))).ToArray()};w.Write(Path.Combine(temp,"tone.wav"));var rw=Wave.Read(Path.Combine(temp,"tone.wav"));Check(rw.Rate==24000&&rw.Samples.SequenceEqual(w.Samples),"WAV PCM exact roundtrip");Check(w.Resample(22050).Samples.Length==22050,"Breath sample rate conversion");
  if(args.Length>0){
   var engine=new KokoroEngine{Root=Path.GetFullPath(args[0]),Threads=2};engine.CheckFiles();
   var original=engine.Generate(cues[0].Text,1,Path.Combine(temp,"normal.wav"));var fast=engine.Generate(cues[0].Text,1.3,Path.Combine(temp,"fast.wav"));Check(fast.Duration<original.Duration,"Kokoro native speed changes measured duration");
   string dest=Renderer.Render(cues,engine,new RenderOptions{Locked=true,Breathing=BreathMode.None},Path.Combine(temp,"result.wav"),(p,m)=>{});var master=Wave.Read(dest);Check(Math.Abs(master.Duration-6.75)<1.0/master.Rate,"Locked master ends at exact original SRT time");Check(Scripts.ToSrt(cues)==Scripts.ToSrt(roundtrip),"Locked rendering preserves timecodes and text");
   int gapA=(int)(3.3*master.Rate),gapB=(int)(3.45*master.Rate);Check(master.Samples.Skip(gapA).Take(gapB-gapA).All(x=>x==0),"Explicit 150ms gap remains clear, no overlap");Check(File.Exists(dest)&&!Directory.GetFiles(temp,"*.csv").Any()&&!Directory.GetDirectories(temp).Any()&&cues.All(c=>c.AudioPath==null),"Only final WAV exported; cue WAVs and reports not retained");
   var flexible=Scripts.ParseText("Have you ever forgotten why you entered a room? Your brain may have switched contexts at the doorway.");string fd=Renderer.Render(flexible,engine,new RenderOptions{Locked=false,Breathing=BreathMode.None},Path.Combine(temp,"result.wav"),(p,m)=>{});Check(Math.Abs(flexible[1].Start-flexible[0].End-.15)<1e-8,"Flexible timeline derives voice duration and 150ms gap");Check(Math.Abs(Wave.Read(fd).Duration-flexible.Last().End)<.001,"Flexible WAV matches generated SRT");
   var impossible=Scripts.ParseSrt("1\n00:00:00,000 --> 00:00:00,200\nHave you ever forgotten why you entered a room?\n");bool rejected=false;try{Renderer.Render(impossible,engine,new RenderOptions{Locked=true,Breathing=BreathMode.None},Path.Combine(temp,"result.wav"),(p,m)=>{});}catch{rejected=true;}Check(rejected&&impossible[0].Status.StartsWith("QUÁ"),"Oversize voice rejected instead of truncating or overlapping");
   engine.Libri=true;engine.Speaker=903;engine.CheckFiles();var last=engine.Generate("This is an offline voice test.",1,Path.Combine(temp,"libri903.wav"));Check(last.Duration>0&&last.Rate==22050,"LibriTTS highest speaker ID 903 works");
   engine.Cancelled=true;Reject(()=>engine.Generate("Cancel",1,Path.Combine(temp,"cancel.wav")),"Cancellation stops further synthesis");
  }
  Console.WriteLine("TOTAL: "+count+" passed. Output: "+temp);
 }
}
