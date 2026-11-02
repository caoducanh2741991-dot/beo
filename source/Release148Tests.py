"""Validate exact release ZIP and upgrades using both new and installed updaters."""
from pathlib import Path
import hashlib,importlib.util,shutil,subprocess,tempfile,xml.etree.ElementTree as ET,zipfile
ROOT=Path(__file__).resolve().parent.parent
PACKAGE=ROOT.parent/'output-1.4.8/V15VoiceStudio_1.4.8_Update.zip'
spec=importlib.util.spec_from_file_location('validation',ROOT/'source/run_validation.py')
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 work=Path(tempfile.mkdtemp(prefix='v15-148-release-'))
 with zipfile.ZipFile(PACKAGE) as z:
  assert z.testzip() is None
  assert len(z.namelist())==len(set(z.namelist()))
  manifest=ET.fromstring(z.read('manifest.xml'));assert manifest.attrib['version']=='1.4.8'
  assert manifest.attrib['kind']=='core'
  for f in manifest:
   name=f.attrib['path'];data=z.read('payload/'+name)
   assert not name.startswith(('ai/models/','ai/runtime/','ai/gpu/','engine/','user_voices/'))
   assert len(data)==int(f.attrib['size']) and hashlib.sha256(data).hexdigest()==f.attrib['sha256']
  assert z.read('payload/source/media/ffmpeg.exe')[:2]==b'MZ'
  assert z.read('payload/source/media/ffmpeg-8.1.3.tar.xz')==(ROOT/'source/media/ffmpeg-8.1.3.tar.xz').read_bytes()
  assert z.read('payload/ai/worker.py')==(ROOT/'source/ai_worker.py').read_bytes()
  assert z.read('V15Updater.exe')==z.read('payload/V15Updater.exe')
  assert b'--restart' in z.read('CAP_NHAT.cmd') and b'\r\n' in z.read('CAP_NHAT.cmd')
  assert PACKAGE.stat().st_size<20*1024*1024
  assert 'payload/source/asr/asr_worker.py' in z.namelist()
  assert all(not name.endswith(('.mp3','.mp4','.wav')) for name in z.namelist())
  z.extractall(work/'bootstrap')
 for source in ['ai_worker.py','gpu_policy.py','NativeGpu.cs','ExternalSpeech.cs']:
  assert (ROOT/'source'/source).read_bytes()==(ROOT.parent/'V15VoiceStudio-1.4.7/source'/source).read_bytes(),source
 print('PASS TTS and GPU implementation byte-identical to 1.4.7')
 print('PASS ZIP CRC, unique paths, manifest 1.4.8, every payload hash, exact production worker, bootstrap parity and Windows command line endings; no model/runtime replacement')
 preserve={'ai/ffmpeg.exe':b'KEEP ORIGINAL AUDIO TOOL','ai/runtime/python.exe':b'KEEP PYTHON','ai/models/qwen_base/model.safetensors':b'KEEP MODEL','ai/gpu/site-packages/torch/lib/torch_cuda.dll':b'KEEP TORCH GPU','engine/sherpa-onnx-offline-tts.exe':b'KEEP CPU','engine/gpu/cudnn64_9.dll':b'KEEP ONNX GPU','user_voices/voices.xml':b'KEEP VOICE METADATA','user_voices/sample.wav':b'KEEP REFERENCE','scripts/source.srt':b'KEEP SRT','scripts/source.wav':b'KEEP WAV','session.xml':b'KEEP SESSION','queue.xml':b'KEEP QUEUE','voice_profiles.tsv':(ROOT/'voice_profiles.tsv').read_bytes()}
 baselines=[('1.4.0',ROOT.parent/'recover-143-fix/installer/$_12_/V15VoiceStudio.exe'),('1.4.3',ROOT.parent/'baseline-1.4.3/payload/V15VoiceStudio.exe'),('1.4.3.1',ROOT.parent/'V15VoiceStudio-1.4.3.1/V15VoiceStudio.exe'),('1.4.4',ROOT.parent/'V15VoiceStudio-1.4.4/V15VoiceStudio.exe'),('1.4.5',ROOT.parent/'V15VoiceStudio-1.4.5/V15VoiceStudio.exe'),('1.4.6',ROOT.parent/'V15VoiceStudio-1.4.6/V15VoiceStudio.exe'),('1.4.7',ROOT.parent/'V15VoiceStudio-1.4.7/V15VoiceStudio.exe')]
 cases=[(version,path,mode,work/'bootstrap/V15Updater.exe') for version,path in baselines for mode in ['folder','zip']]

 for version,baseline in baselines:
  updater=ROOT.parent/('V15VoiceStudio-'+version)/'V15Updater.exe'
  if updater.exists():cases.append((version,baseline,'zip',updater))

 for i,(version,baseline,mode,updater) in enumerate(cases):
  target=work/('installed '+version+' '+mode+' '+str(i));target.mkdir();shutil.copyfile(baseline,target/'V15VoiceStudio.exe')
  for rel,data in preserve.items():
   p=target/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
  r=subprocess.run([v.MONO,str(updater),'--apply-cli','--'+mode,str(work/'bootstrap' if mode=='folder' else PACKAGE),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=40)
  assert r.returncode==0,r.stdout+r.stderr
  for f in manifest:assert digest(target/f.attrib['path'])==f.attrib['sha256']
  for rel,data in preserve.items():assert (target/rel).read_bytes()==data,rel
  assert not list(target.glob('.v15-stage-*')) and not list(target.glob('.v15-backup-*'))
  assert not (target/'.v15-update-journal.xml').exists()
  print('PASS',version,mode,('installed updater' if i>=10 else 'new bootstrap updater'),'; all payload hashes match;',len(preserve),'existing model/runtime/voice/catalog/settings/script fixtures preserved')
 old=ROOT.parent/'output-1.4.7/V15VoiceStudio_1.4.7_Update.zip';before=digest(target/'V15VoiceStudio.exe')
 r=subprocess.run([v.MONO,str(ROOT/'V15Updater.exe'),'--apply-cli','--zip',str(old),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=40)
 assert r.returncode!=0 and 'cũ hơn' in r.stderr and digest(target/'V15VoiceStudio.exe')==before
 print('PASS downgrade to 1.4.7 rejected without replacing 1.4.8')
 v.compile('FrameworkCompatibilityTests',['FrameworkCompatibilityTests.cs'],main='FrameworkCompatibilityTests')
 for name in ['V15VoiceStudio.exe','V15Updater.exe']:
  r=subprocess.run([v.MONO,str(ROOT/'FrameworkCompatibilityTests.exe'),'--scan',str(target/name)],env=v.ENV,capture_output=True,text=True,timeout=30)
  assert r.returncode==0 and 'VERSION=1.4.8.0 UNSUPPORTED=0' in r.stdout,r.stdout+r.stderr
  print('PASS installed',name,'version 1.4.8.0; no unsupported String.Contains(Char) IL calls')
 print('RELEASE 1.4.8 PASS (Mono/Linux + Microsoft .NET Framework 4.5 reference assemblies; physical Windows/GPU not tested)')
 print('ZIP_SHA256',digest(PACKAGE),'BYTES',PACKAGE.stat().st_size)
if __name__=='__main__':main()
