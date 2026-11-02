"""Build Windows .NET binaries and validate behavior using Mono; no hardware claims."""
from pathlib import Path
import os,shutil,subprocess,sys,tempfile,concurrent.futures,socket,time
ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'source';TOOLS=Path('/tmp/v15-tools')
ENV=dict(os.environ,MONO_CFG_DIR=str(TOOLS/'etc'),MONO_PATH=str(TOOLS/'usr/lib/mono/4.5')+':'+str(ROOT),LD_LIBRARY_PATH=str(TOOLS/'usr/lib')+':'+str(TOOLS/'usr/lib/x86_64-linux-gnu'))
MONO=str(TOOLS/'usr/bin/mono');MCS=str(TOOLS/'usr/lib/mono/4.5/mcs.exe')
APP='App.cs Core.cs Batch.cs VoiceCatalog.cs ProjectState.cs UserVoices.cs ExternalSpeech.cs Enhancements.cs AudioProcessing.cs DeviceAndUpdates.cs NativeGpu.cs Punctuation.cs PacingControls.cs AudioFinishing.cs VoiceRhythm.cs QualityControls.cs'.split()
REF_DIR=Path(os.environ.get('V15_NET45_REFS','/tmp/v15-net45/reference/build/.NETFramework/v4.5'))
REFS=['-nostdlib','-noconfig']+['-r:'+str(REF_DIR/(n+'.dll')) for n in ['mscorlib','System','System.Core','System.Xml','System.Xml.Linq','System.Drawing','System.Windows.Forms']]
VALID=ROOT/'validation';VALID.mkdir(exist_ok=True)
def compile(name,files,main=None,app=False):
 args=[MONO,MCS,'-platform:x64','-out:'+str(ROOT/(name+'.exe'))]+REFS
 if main:args+=['-main:'+main]
 if app:args+=['-r:'+str(ROOT/'V15VoiceStudio.exe')]
 if 'Updater' in name:args+=['-r:'+str(REF_DIR/'System.IO.Compression.dll'),'-r:'+str(REF_DIR/'System.IO.Compression.FileSystem.dll')]
 args += [str(SOURCE/f) for f in files]
 subprocess.run(args,env=ENV,check=True,capture_output=True,text=True)
def run(name,args=None,env=None,timeout=90):
 r=subprocess.run([MONO,str(ROOT/(name+'.exe'))]+(args or []),env=env or ENV,capture_output=True,text=True,timeout=timeout)
 (VALID/(name+'.txt')).write_text(r.stdout+r.stderr)
 if r.returncode:raise RuntimeError(name+'\n'+r.stdout+r.stderr)
 print(name, r.stdout.strip().splitlines()[-1],flush=True)
def fixture():
 d=Path(tempfile.mkdtemp(prefix='v15-native-test-'))
 script='''#!/usr/bin/python3
import os,sys,wave,struct,math,time
from pathlib import Path
root=Path(__file__).resolve().parents[2] if Path(__file__).parent.name=='gpu' else Path(__file__).resolve().parents[1]
gpu=Path(__file__).parent.name=='gpu'
with (root/'calls').open('a') as f:f.write(('gpu ' if gpu else 'cpu ')+' '.join(sys.argv[1:])+'\\n')
if (root/'delay').exists():time.sleep(10)
if gpu and (root/'error').exists():sys.exit(7)
if gpu and (root/'fallback').exists():print('Fallback to cpu!',flush=True)
out=next(s.split('=',1)[1] for s in sys.argv if s.startswith('--output-filename='))
with wave.open(out,'wb') as w:
 w.setparams((1,2,24000,0,'NONE','not compressed'))
 w.writeframes(b''.join(struct.pack('<h',int(5000*math.cos(i*.0576))) for i in range(9600)))
'''
 for path in [d/'engine/sherpa-onnx-offline-tts',d/'engine/gpu/sherpa-onnx-offline-tts']:
  path.parent.mkdir(parents=True,exist_ok=True);path.write_text(script);path.chmod(0o755)
 return d
def ui():
 port=36538;display=port-6000
 # X server needs xkbcomp at its compiled location.
 link=Path('/usr/bin/xkbcomp')
 if not link.exists():link.symlink_to(TOOLS/'usr/bin/xkbcomp')
 x=subprocess.Popen([str(TOOLS/'usr/bin/Xvfb'),':'+str(display),'-screen','0','1500x1000x24','-ac','-nolisten','unix','-nolisten','local','-listen','tcp'],env=ENV,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
 try:
  for _ in range(60):
   try:
    with socket.create_connection(('127.0.0.1',port),.2):break
   except OSError:time.sleep(.1)
  else:raise RuntimeError('Xvfb failed '+x.stderr.read().decode())
  for name in ['UiTests','Ui145Tests']:
   data=tempfile.mkdtemp(prefix='v15-ui-state-');run(name,env=dict(ENV,DISPLAY='127.0.0.1:'+str(display),XDG_DATA_HOME=data),timeout=45)
 finally:x.terminate();x.wait(timeout=5)
def main():
 subprocess.run([MONO,MCS,'-target:winexe','-platform:x64','-out:'+str(ROOT/'V15VoiceStudio.exe')]+REFS+[str(SOURCE/x) for x in APP],env=ENV,check=True)
 subprocess.run([MONO,MCS,'-target:winexe','-platform:x64','-out:'+str(ROOT/'V15Updater.exe')]+REFS+['-r:'+str(REF_DIR/'System.IO.Compression.dll'),'-r:'+str(REF_DIR/'System.IO.Compression.FileSystem.dll'),str(SOURCE/'Updater.cs')],env=ENV,check=True)
 specs={'Tests':['Tests.cs'],'BatchTests':['BatchTests.cs'],'FeatureTests':['FeatureTests.cs'],'Update141Tests':['Update141Tests.cs'],'Update145Tests':['Update141Tests.cs','Update145Tests.cs'],'BreathStreamingTests':['Update141Tests.cs','BreathStreamingTests.cs'],'PunctuationTests':['PunctuationTests.cs'],'NativeGpuTests':['NativeGpuTests.cs'],'UiTests':['UiTests.cs'],'Ui145Tests':['Ui145Tests.cs']}
 for name,files in specs.items():compile(name,files,main=name,app=True)
 compile('UpdaterTests',['Updater.cs','UpdaterTests.cs'],main='UpdaterTests')
 compile('OnnxUpdaterTests',['Updater.cs','OnnxUpdaterTests.cs'],main='OnnxUpdaterTests')
 non_ui=[k for k in specs if not k.startswith('Ui')]+['UpdaterTests','OnnxUpdaterTests']
 native=fixture()
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
  jobs=[pool.submit(run,k,[str(native)] if k=='NativeGpuTests' else [str(ROOT.parent)] if k=='Update145Tests' else []) for k in non_ui]
  for job in concurrent.futures.as_completed(jobs):job.result()
 for name in ['test_gpu_policy.py','test_worker_probe.py']:
  r=subprocess.run([sys.executable,str(SOURCE/name)],capture_output=True,text=True,timeout=30);(VALID/(name+'.txt')).write_text(r.stdout+r.stderr)
  if r.returncode:raise RuntimeError(r.stdout+r.stderr)
  print(name,'PASS',flush=True)
 ui()
 print('VALIDATION COMPLETE (Mono/Linux; physical Windows GPU not tested)',flush=True)
if __name__=='__main__':main()
