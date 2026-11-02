"""Verify payload hashes and upgrade 2.0.7 through installed and new updater."""
from pathlib import Path
import hashlib, tempfile, shutil, subprocess, sys, zipfile, xml.etree.ElementTree as ET
sys.path.insert(0,str(Path(__file__).parent))
import run_validation as v
root=v.ROOT
package=root.parent/'output-2.0.8/V15VoiceStudio_2.0.8_Update.zip'
base=root.parent/'baseline208/extracted/payload'
with zipfile.ZipFile(package) as z:
 assert z.testzip() is None
 manifest=ET.fromstring(z.read('manifest.xml'));assert manifest.attrib['version']=='2.0.8'
 for entry in manifest:
  path=entry.attrib['path']; data=z.read('payload/'+path)
  assert len(data)==int(entry.attrib['size']) and hashlib.sha256(data).hexdigest()==entry.attrib['sha256'],path
  assert path not in ['voice_profiles.tsv','voices.bin']
 print('PASS ZIP CRC and every payload hash')
 folder=Path(tempfile.mkdtemp(prefix='v15-release208-package-'));z.extractall(folder)
keep=['ai/runtime/python.exe','ai/models/weights.bin','ai/runtime/torch.dll','engine/gpu/onnxruntime.dll','model/model.onnx','breaths/custom.wav','libritts/voice.bin','kitten/model.onnx','piper_john/model.onnx','piper_kristin/model.onnx','voice_profiles.tsv','voices.bin','user_voices/voices.xml','vietnamese_pack/state.dat','subtitles/config.xml','creator/project.xml','creator-videos.xml','settings.xml','example.srt','example.wav']
for updater in [base/'V15Updater.exe',root/'V15Updater.exe']:
 for pack in [package,folder]:
  target=Path(tempfile.mkdtemp(prefix='v15-release208-installed-'))
  for name in ['V15VoiceStudio.exe','V15Updater.exe']:shutil.copyfile(base/name,target/name)
  for name in keep:
   p=target/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(('PRESERVE '+name).encode())
  before={name:hashlib.sha256((target/name).read_bytes()).hexdigest() for name in keep}
  r=subprocess.run([v.MONO,str(updater),'--apply-cli','--zip' if pack==package else '--folder',str(pack),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=90)
  assert r.returncode==0,r.stdout+r.stderr
  for name in ['V15VoiceStudio.exe','V15Updater.exe']:assert (target/name).read_bytes()==(root/name).read_bytes()
  for name,digest in before.items():assert hashlib.sha256((target/name).read_bytes()).hexdigest()==digest,name
  print('PASS',updater.parent.name,'ZIP' if pack==package else 'folder','upgrade installs final binaries; preserves',len(keep),'data/settings files')
  r=subprocess.run([v.MONO,str(root/'V15Updater.exe'),'--apply-cli','--zip',str(root.parent/'baseline208/V15VoiceStudio_2.0.7_Update.zip'),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=90)
  assert r.returncode!=0 and (target/'V15VoiceStudio.exe').read_bytes()==(root/'V15VoiceStudio.exe').read_bytes()
  print('PASS downgrade to 2.0.7 is rejected')
print('RELEASE 2.0.8 VERIFIED; physical Windows/GPU not exercised')
