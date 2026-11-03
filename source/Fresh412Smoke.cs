using System;using System.IO;using System.Linq;using System.Text;using System.Drawing;using System.Windows.Forms;using System.Collections.Generic;using V15Voice;
public static class Fresh412Smoke {
 static int checks;static void Check(bool ok,string note){if(!ok)throw new Exception(note);checks++;Console.WriteLine("PASS "+note);}
 static KokoroEngine Engine(string root,int sid){return new KokoroEngine{Root=root,Kind=EngineKind.Kokoro,Speaker=sid,Threads=2,DeviceMode="cpu",VoiceName=sid==11?"Adam":"Jessica"};}
 static RenderOptions Options(string path,bool two){return new RenderOptions{Prose=true,Locked=false,WorkflowMode=1,Breathing=BreathMode.None,Punctuation=PunctuationMode.Auto,NormalizeLoudness=false,Gap=.15,TwoVoices=two,VoiceAKey="K:11",VoiceBKey="K:4",ReportContext=new VoiceReportContext{SourcePath=path}};}
 [STAThread] public static int Main(string[] args){try{
  string root=AppDomain.CurrentDomain.BaseDirectory,proof=args.Length>0?args[0]:Path.Combine(root,"kiem-tra-ket-qua");Directory.CreateDirectory(proof);
  Check(Environment.OSVersion.Platform==PlatformID.Win32NT,"Actual Windows process");
  Check(typeof(KokoroEngine).Assembly.GetName().Version.ToString()=="4.1.2.0","Tested Lite core binary version");
  var catalog=new VoiceCatalog(Path.Combine(root,"voice_profiles.tsv"),Path.Combine(proof,"notes.tsv"));
  Check(catalog.All.Count()==20,"Twenty installed English Kokoro voices are catalogued");
  Check(catalog.Get("K:11").Speaker==11&&catalog.Get("K:11").Name.Contains("Adam"),"Adam maps to am_adam SID 11");
  Check(!Directory.Exists(Path.Combine(root,"source/local-ai/model"))&&!Directory.Exists(Path.Combine(root,"ai/models")),"No rewrite LLM or unannounced extra TTS models in fresh edition");
  using(var engine=Engine(root,11)){
   engine.CheckFiles();var wave=engine.Generate("This is a real offline voice test with Adam.",1,Path.Combine(proof,"adam.wav"));
   Check(wave.Duration>1&&wave.Duration<15,"Real Windows Kokoro creates non-empty spoken audio");
   Check(wave.Samples.Any(x=>Math.Abs((int)x)>100),"Audio contains a non-silent waveform");
  }
  string source=Path.Combine(proof,"single.txt");File.WriteAllText(source,"The original words stay unchanged. Adam reads this script offline.",new UTF8Encoding(false));
  var job=BatchJob.Load(source);WorkflowResult single;
  using(var e=Engine(root,11))single=WorkflowRunner.Run(job.Cues,e,Options(source,false),Path.Combine(proof,"single.wav"),1,null,(p,m)=>Console.WriteLine(p+" "+m));
  Check(single.Status=="FINAL_VERIFIED"&&single.Rendered,"Actual source-only TXT pipeline creates verified WAV");
  Check(single.Bundle.Reports.Length==1&&File.Exists(single.Bundle.Primary),"One report for a successful source-only render");
  Check(Scripts.ParseSrt(File.ReadAllText(single.Bundle.Srt)).Select(c=>c.Text).SequenceEqual(single.Cues.Select(c=>c.Text)),"Final SRT matches the text actually rendered");
  string two=Path.Combine(proof,"two.txt");File.WriteAllText(two,"[A] Adam reads the first part of this story.\n[B] Jessica reads the second part of this story.",new UTF8Encoding(false));
  job=BatchJob.Load(two);WorkflowResult pair;
  using(var engine=new TwoVoiceEngine(Engine(root,11),Engine(root,4),"K:11","K:4",job.Cues))pair=WorkflowRunner.Run(job.Cues,engine,Options(two,true),Path.Combine(proof,"two.wav"),1,null,(p,m)=>Console.WriteLine(p+" "+m));
  Check(pair.Status=="FINAL_VERIFIED"&&pair.Rendered,"Two actual installed voices create a verified WAV");
  Check(pair.Bundle.Reports.Length==1&&Scripts.ParseSrt(File.ReadAllText(pair.Bundle.Srt)).Select(c=>c.Text).SequenceEqual(pair.Cues.Select(c=>c.Text)),"Two-voice SRT and report stay consistent");
  Application.EnableVisualStyles();using(var form=new MainForm()){
   form.Show();Application.DoEvents();Check(form.Text.Contains("4.1.2")&&form.Text.Contains("Lite"),"Actual WinForms UI opens from the fresh installation");
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(proof,"fresh-windows-ui.png"));}
   form.Close();
  }
  File.WriteAllText(Path.Combine(proof,"result.txt"),"PASS "+checks+" checks. Actual Windows CPU/Kokoro/FFmpeg and UI; no physical RTX 2070 or clone model test.\r\n");
  Console.WriteLine("TOTAL FRESH WINDOWS "+checks+" PASS");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
