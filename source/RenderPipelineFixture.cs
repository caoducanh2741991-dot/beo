// Test/benchmark helper only. Never compiled into the app. No real voice model.
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Diagnostics;using V15Voice;
public static class RenderPipelineFixture {
 public static string Root(){string root=Path.Combine(Path.GetTempPath(),"beo pipeline fixture "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"ai/models/chatter_nano"));foreach(string name in new[]{"t3_nano_v1.safetensors","s3gen_meanflow.safetensors","ve.safetensors","conds.pt"})File.WriteAllText(Path.Combine(root,"ai/models/chatter_nano",name),"fixture");File.WriteAllText(Path.Combine(root,"ai/ffmpeg.exe"),"Linux fixture uses real ffmpeg");
  File.WriteAllText(Path.Combine(root,"ai/worker.py"),@"import os,sys,time,base64,wave,struct,math
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
time.sleep(0.15) # synthetic cold-model cost; not a GPU measurement
with open(os.path.join(root,'starts'),'a') as f:f.write(str(os.getpid())+'\n')
with open(os.path.join(root,'pid'),'w') as f:f.write(str(os.getpid()))
def emit(kind,text):print('V15_'+kind+'\t'+base64.b64encode(text.encode()).decode(),flush=True)
emit('READY','CPU synthetic pipeline fixture')
for line in sys.stdin:
 p=line.rstrip('\n').split('\t')
 if not p or p[0]!='SYNTH':continue
 text=base64.b64decode(p[1]).decode();out=base64.b64decode(p[2]).decode();speed=float(p[3])
 with open(os.path.join(root,'requests'),'a') as f:f.write(text+'\n')
 with wave.open(out,'wb') as w:
  w.setparams((1,2,24000,0,'NONE','PCM'))
  w.writeframes(b''.join(struct.pack('<h',int(5000*math.sin(i*.063))) for i in range(int(3*24000/speed))))
 emit('OK','CPU synthetic pipeline fixture')
");
  string probe=Path.Combine(root,"ffmpeg-probe.py");File.WriteAllText(probe,"#!/usr/bin/python3\nimport os,sys\nfrom pathlib import Path\nroot=Path(__file__).parent\nwith (root/'ffmpeg-calls').open('a') as f:f.write('atempo\\n' if any('atempo=' in x for x in sys.argv) else 'other\\n')\nos.execv('/usr/bin/ffmpeg',['/usr/bin/ffmpeg']+sys.argv[1:])\n");using(var p=Process.Start("chmod","+x "+KokoroEngine.Q(probe)))p.WaitForExit();Environment.SetEnvironmentVariable("V15_FFMPEG",probe);return root;
 }
 public static KokoroEngine Engine(string root,string state=null,string role="A"){return new KokoroEngine{Root=root,Kind=EngineKind.Chatterbox,ModelFolder="chatter_nano",VoiceName="fixture-"+role,DeviceMode="cpu",Archive=state==null?null:new SpeechArchive(state)};}
 public static VoiceSettings Settings(){return new VoiceSettings{Mode=0,WorkflowMode=2,AiMode=2,Breathing=(int)BreathMode.None,Normalize=false,GentleFit=false,Slow=false,Gap=0,Punctuation=0,Speed=1,MinSpeed=.9,MaxSpeed=1.25};}
 public static string Source(string root,int count=8,double budget=2.7){var cues=new List<Cue>();for(int i=0;i<count;i++)cues.Add(new Cue{Id=i+1,Start=i*(budget+.15),End=i*(budget+.15)+budget,Text="This original source sentence keeps its meaning number "+i+"."});string path=Path.Combine(root,"story.srt");File.WriteAllText(path,Scripts.ToSrt(cues));return path;}
 public static RenderOptions Options(string root,string source){var o=AudioRepair.Options(Settings().Xml().ToString());o.ReportContext=new VoiceReportContext{SourcePath=source};o.CheckpointRoot=Path.Combine(root,"pcm");var flag=o.GetType().GetField("ReuseLockedAudio");if(flag!=null)flag.SetValue(o,true);return o;}
 public sealed class NeverRewrite:ITextRewriter {public int Calls;public string Rewrite(string prompt,Func<bool> cancel,Action<string> progress){Calls++;throw new Exception("Unexpected model call for a passing fixture");}}
 public static WorkflowResult Run(string root,string path,int mode,out RenderOptions o){string state=Path.Combine(root,"state");o=Options(root,path);var options=o;var ai=new NeverRewrite();using(var e=Engine(root,state))return WorkflowRunner.Run(BatchJob.Load(path).Cues,e,o,OutputFiles.Beside(path),mode,mode==1?null:(Func<List<Cue>,RenderOptions,string,KokoroEngine,Action<int,string>,List<Cue>>)((words,opts,source,owner,notify)=>{var settings=Settings();var p=AiRepairSession.Open(source,words,state,settings,false,"pipeline-fixture");opts.AiRepair=p;opts.AiReportPath=p.Report;owner.ReleaseModels();AiTimeline.Run(p,ai,role=>Engine(root,state,role),()=>owner.Cancelled,notify,true,true,0,true);return p.Effective();}),(p,m)=>{});}
 public static int Lines(string root,string name){string p=Path.Combine(root,name);return File.Exists(p)?File.ReadAllLines(p).Length:0;}
 public static bool Alive(string root){string path=Path.Combine(root,"pid");if(!File.Exists(path))return false;try{using(var p=Process.GetProcessById(Int32.Parse(File.ReadAllText(path))))return !p.HasExited;}catch(ArgumentException){return false;}}
}
