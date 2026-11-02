using System;using System.IO;using System.Linq;using V15Voice;
public static class GpuFallbackTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
 public static void Main(string[] args){string root=Path.GetFullPath(args[0]);string log=Path.Combine(root,"ai","starts.txt"),scenario=Path.Combine(root,"ai","scenario.txt"),output=Path.Combine(root,"out.wav");
  foreach(string test in new[]{"oom","startup","unrelated","forced"}){
   File.WriteAllText(scenario,test);if(File.Exists(log))File.Delete(log);using(var e=new KokoroEngine{Root=root,Kind=EngineKind.Qwen,ModelFolder="qwen_custom",DeviceMode=test=="forced"?"gpu":"auto"}){
    bool success=false;try{var w=e.Generate("New narration.",1,output);success=w.Duration==1;}catch(Exception ex){Check(ex.Message.Contains(test=="unrelated"?"missing reference":"GPU_FALLBACK"),test+" retains actionable error");}
    var starts=File.ReadAllLines(log);if(test=="oom"||test=="startup"){Check(success&&starts.SequenceEqual(new[]{"auto","cpu"}),test+" restarts a fresh CPU worker exactly once");Check(e.DeviceInfo.Contains("CPU dự phòng"),test+" visibly reports CPU fallback");}else Check(!success&&starts.Length==1,test+" does not silently fall back or retry model errors");
   }
  }Console.WriteLine("TOTAL GPU FALLBACK "+count+" passed.");
 }
}
