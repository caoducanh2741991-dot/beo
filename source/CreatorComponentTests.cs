using System;using System.IO;using V15Voice;
public static class CreatorComponentTests {
 static int n;static void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;Console.WriteLine("PASS "+why);}
 sealed class NoAi:ITextRewriter {public int Calls;public string Rewrite(string p,Func<bool> c,Action<string> a){Calls++;throw new Exception("AI must not run for missing video components");}}
 public static int Main(){try{string queue=File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"validation","creator-root.txt"));var p=CreatorStore.Load(queue)[0];p.OutputFolder=Path.Combine(Path.GetDirectoryName(queue),"minimal-audio-components");p.State="QUEUED";p.RequireVietnamese=false;p.ExportMp4=false;
  var media=CreatorMedia.Probe("",p.Video,()=>false);Check(media.Width==360&&media.Height==640&&media.Duration==6.5,"Same component selection as bundled audio-only FFmpeg still probes H264 MP4 by packet copy");
  Check(!CreatorMedia.Capabilities("",()=>false).Mux,"Audio-only component build correctly reports no MP4/AAC encoder capability");
  CreatorPipeline.Export("",p,Path.Combine(Path.GetDirectoryName(queue),"no-translations"),null,()=>false,(x,m)=>{});Check(p.State=="PACKAGE_EXPORTED"&&File.Exists(p.Zip),"Editing package works with bundled audio component set, without a full video runtime");
  p.ExportMp4=true;p.State="QUEUED";p.RequireVietnamese=true;p.Translation=null;var ai=new NoAi();bool rejected=false;try{CreatorPipeline.Export("",p,Path.Combine(Path.GetDirectoryName(queue),"no-translations"),ai,()=>false,(x,m)=>{});}catch(Exception ex){rejected=ex.ToString().Contains("CREATOR_TOOLS_REQUIRED");}Check(rejected&&ai.Calls==0&&p.State=="NEEDS_REVIEW","MP4 component absence is caught before spending time or VRAM on translation");
  Console.WriteLine("TOTAL CREATOR COMPONENTS "+n+" PASS · native Linux FFmpeg built with the shipped Windows audio-only component switches; not Windows PE execution");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
