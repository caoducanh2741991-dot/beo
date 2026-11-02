"""Verify the final ZIP through both the installed 1.4.12 and new updater."""
from pathlib import Path
import hashlib,importlib.util,shutil,subprocess,tempfile,zipfile,xml.etree.ElementTree as E
ROOT=Path(__file__).resolve().parent.parent;PACKAGE=ROOT.parent/'output-2.0/V15VoiceStudio_2.0_Update.zip';BASE=ROOT.parent/'baseline-1.4.12/payload'
spec=importlib.util.spec_from_file_location('v',ROOT/'source/run_validation.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 work=Path(tempfile.mkdtemp(prefix='v1411-release-'))
 with zipfile.ZipFile(PACKAGE) as z:
  assert z.testzip() is None and len(z.namelist())==len(set(z.namelist()));manifest=E.fromstring(z.read('manifest.xml'));assert manifest.get('version')=='2.0.0' and manifest.get('kind')=='core'
  for f in manifest:
   name=f.get('path');data=z.read('payload/'+name);assert len(data)==int(f.get('size')) and hashlib.sha256(data).hexdigest()==f.get('sha256')
   assert not name.startswith(('ai/models/','ai/runtime/','ai/gpu/','engine/','user_voices/')) and name!='voice_profiles.tsv'
  model='payload/source/asr/phrase_model/averaged_perceptron_tagger_eng.zip';assert hashlib.sha256(z.read(model)).hexdigest()=='6025f530624335c67d6547d44757b357b4e79bae030a0383e9887a92c1718f0b'
  assert z.read('V15Updater.exe')==z.read('payload/V15Updater.exe');assert z.read('payload/V15VoiceStudio.exe')[:2]==b'MZ';assert b'--restart' in z.read('CAP_NHAT.cmd');assert PACKAGE.stat().st_size<20*1024*1024
  assert not any(n.endswith(('.mp4','.mp3','.wav')) or 'qa-clips' in n or 'refined-user.xml' in n for n in z.namelist());z.extractall(work/'package')
 print('PASS exact ZIP CRC, every payload SHA256, 2.0 manifest, local grammar model hash, bootstrap parity, no voice/model/runtime/catalog replacement',flush=True)
 keep={'ai/models/qwen/model.safetensors':b'KEEP MODEL','ai/runtime/python.exe':b'KEEP RUNTIME','ai/gpu/torch_cuda.dll':b'KEEP CUDA','engine/gpu/cudnn.dll':b'KEEP GPU','user_voices/voices.xml':b'KEEP VOICE NOTES','user_voices/reference.wav':b'KEEP REFERENCE','voice_profiles.tsv':b'KEEP USER CATALOG','session.xml':b'KEEP SETTINGS','queue.xml':b'KEEP QUEUE','scripts/original.srt':b'KEEP SRT','scripts/output.wav':b'KEEP WAV'}
 for updater in [BASE/'V15Updater.exe',ROOT/'V15Updater.exe']:
  for mode in ['zip','folder']:
   target=work/('installed '+updater.parent.name+' '+mode);target.mkdir();shutil.copyfile(BASE/'V15VoiceStudio.exe',target/'V15VoiceStudio.exe')
   for rel,data in keep.items():p=target/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
   cmd=[v.MONO,str(updater),'--apply-cli','--'+mode,str(PACKAGE if mode=='zip' else work/'package'),'--target',str(target)];r=subprocess.run(cmd,env=v.ENV,capture_output=True,text=True,timeout=60);assert r.returncode==0,r.stdout+r.stderr
   for f in manifest:assert digest(target/f.get('path'))==f.get('sha256')
   for rel,data in keep.items():assert (target/rel).read_bytes()==data,rel
   assert not (target/'.v15-update-journal.xml').exists();assert not list(target.glob('.v15-stage-*')) and not list(target.glob('.v15-backup-*'))
   print('PASS upgrade via',('installed 1.4.12' if updater.parent==BASE else 'new 2.0'),mode,'all files verified and 11 voice/runtime/settings/output fixtures preserved',flush=True)
 before=digest(target/'V15VoiceStudio.exe');old=ROOT.parent/'recovered-2.0/V15VoiceStudio_1.4.12_Update.zip';r=subprocess.run([v.MONO,str(ROOT/'V15Updater.exe'),'--apply-cli','--zip',str(old),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=60);assert r.returncode!=0 and 'cũ hơn' in r.stderr and digest(target/'V15VoiceStudio.exe')==before
 print('PASS downgrade to 1.4.12 rejected',flush=True)
 v.compile('FrameworkCompatibilityTests',['FrameworkCompatibilityTests.cs'],main='FrameworkCompatibilityTests')
 for name in ['V15VoiceStudio.exe','V15Updater.exe']:
  r=subprocess.run([v.MONO,str(ROOT/'FrameworkCompatibilityTests.exe'),'--scan',str(target/name)],env=v.ENV,capture_output=True,text=True,timeout=30);assert r.returncode==0 and 'VERSION=2.0.0.0 UNSUPPORTED=0' in r.stdout,r.stdout+r.stderr;print('PASS installed',name,'2.0.0 / .NET Framework compatibility',flush=True)
 print('RELEASE 2.0 PASS',PACKAGE.stat().st_size,digest(PACKAGE),flush=True)
if __name__=='__main__':main()
