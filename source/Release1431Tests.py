"""Test the actual repair ZIP bootstrap, preserved user data, and downgrade guard."""
from pathlib import Path
import hashlib,importlib.util,os,shutil,subprocess,tempfile,xml.etree.ElementTree as ET,zipfile
ROOT=Path(__file__).resolve().parent.parent
PACKAGE=ROOT.parent/'output-1.4.3.1/V15VoiceStudio_1.4.3.1_Update.zip'
spec=importlib.util.spec_from_file_location('validation',ROOT/'source/run_validation.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 work=Path(tempfile.mkdtemp(prefix='v15-1431-release-'))
 with zipfile.ZipFile(PACKAGE) as z:
  assert z.testzip() is None
  assert len(z.namelist())==len(set(z.namelist()))
  manifest=ET.fromstring(z.read('manifest.xml'));assert manifest.attrib['version']=='1.4.3.1'
  for f in manifest:
   data=z.read('payload/'+f.attrib['path']);assert len(data)==int(f.attrib['size']) and hashlib.sha256(data).hexdigest()==f.attrib['sha256']
  assert z.read('V15Updater.exe')==z.read('payload/V15Updater.exe')
  assert b'--restart' in z.read('CAP_NHAT.cmd') and b'\r\n' in z.read('CAP_NHAT.cmd')
  z.extractall(work/'bootstrap')
 print('PASS repair ZIP CRC, hashes, Windows bootstrap line endings and identical bootstrap/payload updater')
 preserve={'ai/runtime/python.exe':b'KEEP PYTHON','ai/models/qwen_base/model.safetensors':b'KEEP MODEL','ai/gpu/site-packages/torch/lib/torch_cuda.dll':b'KEEP TORCH GPU','engine/sherpa-onnx-offline-tts.exe':b'KEEP CPU','engine/gpu/cudnn64_9.dll':b'KEEP ONNX GPU','user_voices/voices.xml':b'KEEP VOICE METADATA','user_voices/sample.wav':b'KEEP REFERENCE','scripts/source.srt':b'KEEP SRT','scripts/source.wav':b'KEEP WAV','session.xml':b'KEEP SESSION','queue.xml':b'KEEP QUEUE'}
 baselines=[('1.4.0',ROOT.parent/'recover-143-fix/installer/$_12_/V15VoiceStudio.exe'),('1.4.3',ROOT.parent/'baseline-1.4.3/payload/V15VoiceStudio.exe')]
 for version,baseline in baselines:
  for mode in ['folder','zip']:
   target=work/('installed '+version+' '+mode);target.mkdir();shutil.copyfile(baseline,target/'V15VoiceStudio.exe')
   for rel,data in preserve.items():p=target/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
   shutil.copyfile(ROOT/'voice_profiles.tsv',target/'voice_profiles.tsv')
   r=subprocess.run([v.MONO,str(work/'bootstrap/V15Updater.exe'),'--apply-cli','--'+mode,str(work/'bootstrap' if mode=='folder' else PACKAGE),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=40)
   assert r.returncode==0,r.stdout+r.stderr
   for f in manifest:assert digest(target/f.attrib['path'])==f.attrib['sha256']
   for rel,data in preserve.items():assert (target/rel).read_bytes()==data,rel
   assert not list(target.glob('.v15-stage-*')) and not list(target.glob('.v15-backup-*'))
   assert not (target/'.v15-update-journal.xml').exists()
   print('PASS',version,mode,'repair through extracted new updater; all payload hashes match and all',len(preserve),'user/model/runtime fixtures preserved')
 old=ROOT.parent/'recover-143-fix/V15VoiceStudio_1.4.3_Update.zip';before=digest(target/'V15VoiceStudio.exe')
 r=subprocess.run([v.MONO,str(ROOT/'V15Updater.exe'),'--apply-cli','--zip',str(old),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=40)
 assert r.returncode!=0 and 'cũ hơn' in r.stderr and digest(target/'V15VoiceStudio.exe')==before
 print('PASS downgrade to original 1.4.3 is rejected without replacing current app')
 (ROOT/'validation/installed-test-path.txt').write_text(str(target))
 print('PASS release 1.4.3.1; Mono behavior tests and Microsoft .NET Framework 4.5 reference compilation; physical Windows not tested')
if __name__=='__main__':main()
