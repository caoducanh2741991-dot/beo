using System;using System.IO;using System.IO.Compression;using System.Linq;using System.Reflection;using System.Diagnostics;using System.Collections.Generic;using System.Security.Cryptography;
public static class InlineAiPackageTests {
 static int n;static void Check(bool v,string s){if(!v)throw new Exception(s);Console.WriteLine("PASS "+s);n++;}
 static string Hash(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s));}
 public static int Main(string[] args){try{
  string package=Path.GetFullPath(args[0]),baseline=Path.GetFullPath(args[1]),updater=Path.GetFullPath(args[2]);
  string dir=Path.Combine(Path.GetTempPath(),"V15 upgrade214 "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  using(var z=ZipFile.OpenRead(baseline))using(var src=z.GetEntry("payload/V15VoiceStudio.exe").Open())using(var dst=File.Create(Path.Combine(dir,"V15VoiceStudio.exe")))src.CopyTo(dst);
  var preserve=new Dictionary<string,string>();foreach(string name in new[]{"ai/runtime/python.exe","ai/models/voice/test.bin","voice_profiles.tsv","references/Adam.wav","source/local-ai/model/qwen3-4b.gguf","source/local-ai/runtime/cpu/llama-completion.exe","engine/gpu/onnxruntime.dll","story.srt","story.wav"}){string p=Path.Combine(dir,name);Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllText(p,"preserve214:"+name);preserve[p]=Hash(p);}
  var assembly=Assembly.LoadFrom(updater);var apply=assembly.GetType("V15Update.UpdateEngine").GetMethod("Apply");apply.Invoke(null,new object[]{package,dir,new Action<string>(s=>{})});
  Check(FileVersionInfo.GetVersionInfo(Path.Combine(dir,"V15VoiceStudio.exe")).FileVersion=="2.0.14.0","Actual updater upgrades 2.0.13 app to 2.0.14");
  Check(preserve.All(x=>Hash(x.Key)==x.Value),"Upgrade preserves installed TTS, AI model/runtime, custom reference, GPU, SRT and WAV");
  Check(File.Exists(Path.Combine(dir,"source/InlineAiControls.cs"))&&File.ReadAllText(Path.Combine(dir,"HUONG_DAN_2.0.14.txt")).Contains("AI sửa câu"),"Installed package contains new implementation and matching guide");
  Check(!File.Exists(Path.Combine(dir,".v15-update-journal.xml")),"Successful upgrade has no incomplete transaction journal");
  using(var z=ZipFile.OpenRead(package))Check(!z.Entries.Any(e=>e.FullName.Contains("source/local-ai/model/")||e.FullName.StartsWith("payload/ai/models/")),"Core update does not bundle or replace installed voice or AI models");
  Console.WriteLine("TOTAL INLINE PACKAGE "+n+" passed with updater "+assembly.GetName().Version+". Mono/Linux fixture install.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
