"""Build a self-contained Kokoro edition, without rewriting or touching old installs.

The other speech engines remain in the application; their models are separate.
No repository write credential is used by this builder or its CI workflow.
"""
from pathlib import Path, PurePosixPath
import argparse, concurrent.futures, hashlib, json, os, shutil, struct
import subprocess, tarfile, time, urllib.request, zipfile
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
WORK = ROOT / 'fresh-build'
STAGE = WORK / 'BEO_Voice_Studio_Lite_4.1.2'
OUT = ROOT / 'dist-fresh412'
NAMES = 'Alloy Aoede Bella Heart Jessica Kore Nicole Nova River Sarah Sky Adam Echo Eric Fenrir Liam Michael Onyx Puck Santa'.split()
ASSETS = [
 {'name':'kokoro-multi-lang-v1_0.tar.bz2','url':'https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2','bytes':349886476,'sha256':'60703707bc05ccd48d60e1eb723f38b07d6e474795a0b6a588d7220400e0f7a4'},
 {'name':'sherpa-onnx-v1.13.8-win-x64-shared-MT-Release.tar.bz2','url':'https://github.com/k2-fsa/sherpa-onnx/releases/download/v1.13.8/sherpa-onnx-v1.13.8-win-x64-shared-MT-Release.tar.bz2','bytes':24805859,'sha256':'6dffdc715a4465b989446a6105265d2cb345e7101591a17d35534b6758f6e8df'},
]

def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for chunk in iter(lambda:f.read(4*1024*1024),b''):h.update(chunk)
 return h.hexdigest()

def download(a):
 cache=WORK/'downloads';cache.mkdir(parents=True,exist_ok=True)
 p=cache/a['name']
 if p.exists() and sha(p)==a['sha256']:return p
 req=urllib.request.Request(a['url'],headers={'User-Agent':'BEO-Voice-Studio-Fresh/4.1.2'})
 tmp=p.with_name(p.name+'.partial')
 with urllib.request.urlopen(req,timeout=90) as r,tmp.open('wb') as f:shutil.copyfileobj(r,f,4*1024*1024)
 if tmp.stat().st_size!=a['bytes'] or sha(tmp)!=a['sha256']:raise RuntimeError('Upstream SHA256/size mismatch: '+a['name'])
 tmp.replace(p);print('VERIFIED',p.name,flush=True);return p

def unpack(archive, destination):
 # Copy each regular entry to completion, then check its declared size.
 # No links or special nodes from an archive are installed.
 destination.mkdir(parents=True,exist_ok=True)
 with tarfile.open(archive,'r|bz2') as t:
  for m in t:
   parts=PurePosixPath(m.name).parts
   if not parts or any(p in ('..','') or ':' in p or '\\' in p for p in parts) or m.name.startswith('/'):
    raise RuntimeError('Unsafe archive member: '+m.name)
   p=destination.joinpath(*parts)
   if m.isdir():p.mkdir(parents=True,exist_ok=True);continue
   if not m.isfile():raise RuntimeError('Unexpected non-file archive member: '+m.name)
   p.parent.mkdir(parents=True,exist_ok=True)
   with t.extractfile(m) as src,p.open('wb') as dst:shutil.copyfileobj(src,dst,4*1024*1024)
   if p.stat().st_size!=m.size:raise RuntimeError('Truncated archive member: '+m.name)

def native_dependencies():
 """Extract app-local Microsoft VC runtime DLLs and its license, no system install."""
 spec=json.loads((ROOT/'source/fresh412-vc-lock.json').read_text())
 exe=download(spec);seven=shutil.which('7z') or r'C:\Program Files\7-Zip\7z.exe'
 base=WORK/'vc';base.mkdir(exist_ok=True)
 data=exe.read_bytes();start=0;cabinet=[]
 while True:
  pos=data.find(b'MSCF\0\0\0\0',start)
  if pos<0:break
  size=struct.unpack_from('<I',data,pos+8)[0];start=pos+8
  if 36<=size<=len(data)-pos:
   cab=base/('bundle-'+str(len(cabinet))+'.cab');cab.write_bytes(data[pos:pos+size]);cabinet.append(cab)
 if len(cabinet)<2:raise RuntimeError('VC redistributable container changed')
 for i,cab in enumerate(cabinet):subprocess.run([seven,'x',str(cab),'-o'+str(base/str(i)),'-y'],check=True,stdout=subprocess.DEVNULL)
 metadata=ET.fromstring((base/'0/0').read_bytes())
 payloads={x.attrib.get('FilePath'):x.attrib.get('SourcePath') for x in metadata.iter() if x.tag.split('}')[-1]=='Payload'}
 name=next(v for k,v in payloads.items() if 'vcruntimeminimum_amd64' in k.lower() and k.lower().endswith('.cab'))
 target=base/'minimum'
 subprocess.run([seven,'x',str(base/'1'/name),'-o'+str(target),'-y'],check=True,stdout=subprocess.DEVNULL)
 files=list(target.rglob('*.dll_amd64'))
 if len(files)<8:raise RuntimeError('Missing VC side-by-side DLLs')
 for f in files:shutil.copyfile(f,STAGE/'engine'/f.name[:-6])
 license_file=next((p for p in (base/'0').iterdir() if p.is_file() and p.read_bytes()[:6]==b'{\\rtf1'),None)
 if license_file is None:raise RuntimeError('Missing VC redistributable license')
 shutil.copyfile(license_file,STAGE/'engine/LICENSE-Microsoft-Visual-Cpp.rtf')

def prepare():
 if os.name!='nt':raise RuntimeError('The fresh release must be built and tested on Windows')
 if STAGE.exists():shutil.rmtree(STAGE)
 STAGE.mkdir(parents=True)
 expected={'V15VoiceStudio.exe':'edf76a5dc7fcf080af66c77b84ff82dd5a259d6a10a6dcc2eb89e00ace6ece53','V15Updater.exe':'0fbafe9d31af88b24b949102c14ec3345165d8c04868a83b8903188622d05c35'}
 for name,digest in expected.items():
  if sha(ROOT/name)!=digest:raise RuntimeError('Unexpected core binary: '+name)
  shutil.copyfile(ROOT/name,STAGE/name)
 shutil.copytree(ROOT/'source',STAGE/'source',ignore=shutil.ignore_patterns('__pycache__','*.pyc','local-ai','validation-*'))
 for name in ['HUONG_DAN_MAY_MOI_4.1.2.txt','HUONG_DAN_4.1.2.txt','KET_QUA_KIEM_THU_4.1.2.txt']:
  shutil.copyfile(ROOT/name,STAGE/name)
 (STAGE/'ai').mkdir();shutil.copyfile(ROOT/'source/ai_worker.py',STAGE/'ai/worker.py');shutil.copyfile(ROOT/'source/gpu_policy.py',STAGE/'ai/gpu_policy.py')
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:archives=list(pool.map(download,ASSETS))
 unpack(archives[0],WORK/'unpacked-model');unpack(archives[1],WORK/'unpacked-engine')
 shutil.copytree(WORK/'unpacked-model/kokoro-multi-lang-v1_0',STAGE/'model')
 engine=WORK/'unpacked-engine/sherpa-onnx-v1.13.8-win-x64-shared-MT-Release'
 (STAGE/'engine').mkdir();shutil.copyfile(engine/'bin/sherpa-onnx-offline-tts.exe',STAGE/'engine/sherpa-onnx-offline-tts.exe')
 for f in (engine/'lib').glob('*.dll'):shutil.copyfile(f,STAGE/'engine'/f.name)
 native_dependencies()
 for f in (ROOT/'source/fresh412-licenses').glob('*'):shutil.copyfile(f,STAGE/'engine'/f.name)
 profiles=json.loads((ROOT/'source/research/release_profiles_1.3.0.json').read_text(encoding='utf-8-sig'))
 rows=[]
 for sid,name in enumerate(NAMES):
  p=next(x for x in profiles if x['Key']=='K:'+str(sid))
  # The official v1.0 metadata maps speaker 11 to am_adam, never fixture SID 3.
  cells=[p['Key'],'Nữ' if sid<11 else 'Nam','Kokoro v1.0 · American English · offline','Kokoro-82M / sherpa-onnx; model license included.','Chưa có','1' if name=='Adam' else '0','Kokoro · '+name,'Kokoro',str(sid),'',p.get('Styles','Kể chuyện'),'Gợi ý phong cách; hãy nghe thử kịch bản thực tế.','','']
  rows.append('\t'.join(str(v).replace('\t',' ').replace('\n',' ') for v in cells))
 (STAGE/'voice_profiles.tsv').write_text('\n'.join(rows)+'\n',encoding='utf-8-sig')
 sample=STAGE/'examples';sample.mkdir()
 (sample/'thu_giong.txt').write_text('This is an offline voice test. Your original script stays unchanged.',encoding='utf-8')
 (sample/'hai_giong.txt').write_text('[A] We can use Adam for the first voice.\n[B] Jessica can read the second voice.',encoding='utf-8')
 (STAGE/'BAT_DAU.cmd').write_bytes(b'@echo off\r\ncd /d "%~dp0"\r\nstart "" "%~dp0V15VoiceStudio.exe"\r\n')
 (STAGE/'fresh-edition.json').write_text(json.dumps({'version':'4.1.2','edition':'Lite Fresh Kokoro','included_models':['Kokoro v1.0'],'english_voices':20,'adam_speaker_id':11,'ai_rewrite':False,'gpu_runtime_bundled':False,'optional_not_bundled':['Chatterbox models','Qwen3-TTS models and clone runtime','LibriTTS','Kitten','Piper','CUDA add-on']},indent=2))
 print('PREPARED',STAGE,flush=True)

def nsis_escape(s):return str(s).replace('$','$$').replace('"','$\\"')

def finish():
 OUT.mkdir(exist_ok=True)
 rows=[{'path':p.relative_to(STAGE).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(STAGE.rglob('*')) if p.is_file()]
 (STAGE/'fresh-manifest.json').write_text(json.dumps({'version':'4.1.2','files':rows,'sources':ASSETS},indent=2))
 archive=OUT/'BEO_Voice_Studio_4.1.2_Fresh_Kokoro_Portable.zip'
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as z:
  for p in sorted(STAGE.rglob('*')):
   if p.is_file():z.write(p,STAGE.name+'/'+p.relative_to(STAGE).as_posix())
 with zipfile.ZipFile(archive) as z:
  if z.testzip() is not None:raise RuntimeError('Portable ZIP CRC failed')
  for row in rows:
   h=hashlib.sha256()
   with z.open(STAGE.name+'/'+row['path']) as f:
    for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
   if h.hexdigest()!=row['sha256']:raise RuntimeError('Portable manifest mismatch: '+row['path'])
 delete='\n'.join(' Delete "$INSTDIR\\'+nsis_escape(r['path'].replace('/','\\'))+'"' for r in rows)+ '\n Delete "$INSTDIR\\fresh-manifest.json"'
 dirs=sorted({p.relative_to(STAGE).as_posix() for p in STAGE.rglob('*') if p.is_dir()},key=lambda n:(-n.count('/'),n))
 delete+='\n'+'\n'.join(' RMDir "$INSTDIR\\'+nsis_escape(n.replace('/','\\'))+'"' for n in dirs)
 setup=OUT/'BEO_Voice_Studio_4.1.2_Fresh_Kokoro_Setup.exe'
 text='''Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
Name "BÉO Voice Studio Lite 4.1.2 · Kokoro"
OutFile "SETUP_PATH"
InstallDir "$LOCALAPPDATA\\BEO Voice Studio Lite"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Vietnamese"
Function .onVerifyInstDir
 IfFileExists "$INSTDIR\\V15VoiceStudio.exe" 0 empty
 MessageBox MB_ICONSTOP "Chọn thư mục mới. Dùng gói Core Update để cập nhật bản đã có."
 Abort
 empty:
FunctionEnd
Section "Kokoro offline"
 SetOutPath "$INSTDIR"
 File /r "STAGE_PATH\\*"
 WriteUninstaller "$INSTDIR\\Uninstall.exe"
 CreateShortcut "$DESKTOP\\BÉO Voice Studio Lite.lnk" "$INSTDIR\\V15VoiceStudio.exe"
 CreateDirectory "$SMPROGRAMS\\BÉO Voice Studio Lite"
 CreateShortcut "$SMPROGRAMS\\BÉO Voice Studio Lite\\BÉO Voice Studio Lite.lnk" "$INSTDIR\\V15VoiceStudio.exe"
 WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BEO-Voice-Studio-Lite" "DisplayName" "BÉO Voice Studio Lite 4.1.2 · Kokoro"
 WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BEO-Voice-Studio-Lite" "DisplayVersion" "4.1.2"
 WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BEO-Voice-Studio-Lite" "UninstallString" '$\\"$INSTDIR\\Uninstall.exe$\\"'
SectionEnd
Section "Uninstall"
DELETE_OWNED
 Delete "$DESKTOP\\BÉO Voice Studio Lite.lnk"
 Delete "$SMPROGRAMS\\BÉO Voice Studio Lite\\BÉO Voice Studio Lite.lnk"
 RMDir "$SMPROGRAMS\\BÉO Voice Studio Lite"
 Delete "$INSTDIR\\Uninstall.exe"
 RMDir "$INSTDIR"
 DeleteRegKey HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BEO-Voice-Studio-Lite"
SectionEnd
'''
 text=text.replace('SETUP_PATH',nsis_escape(setup)).replace('STAGE_PATH',nsis_escape(STAGE)).replace('DELETE_OWNED',delete)
 config=WORK/'fresh412.nsi';config.write_text(text,encoding='utf-8-sig')
 compiler=shutil.which('makensis') or r'C:\Program Files (x86)\NSIS\makensis.exe'
 subprocess.run([compiler,'/V2',str(config)],check=True)
 for p in [archive,setup]:print('PACKAGE',p.name,p.stat().st_size,sha(p),flush=True)

def verify_install():
 target=WORK/'installed-kokoro';installer=OUT/'BEO_Voice_Studio_4.1.2_Fresh_Kokoro_Setup.exe'
 if target.exists():raise RuntimeError('Install verification target must be empty')
 subprocess.run([str(installer),'/S','/D='+str(target)],check=True,timeout=240)
 data=json.loads((target/'fresh-manifest.json').read_text())
 for r in data['files']:
  p=target/r['path']
  if not p.exists() or p.stat().st_size!=r['bytes'] or sha(p)!=r['sha256']:raise RuntimeError('Installed file mismatch: '+r['path'])
 subprocess.run([str(target/'KIEM_TRA.exe'),str(WORK/'installed-proof')],check=True,timeout=300,cwd=target)
 preserved=target/'USER_KEEP.txt';preserved.write_text('User file must survive uninstall')
 subprocess.run([str(target/'Uninstall.exe'),'/S','_?='+str(target)],check=True,timeout=60)
 for _ in range(30):
  if not (target/'V15VoiceStudio.exe').exists():break
  time.sleep(1)
 if (target/'V15VoiceStudio.exe').exists() or not preserved.exists():raise RuntimeError('Uninstall ownership check failed')
 result={'version':'4.1.2','edition':'Fresh Kokoro','fresh_install_verified':True,'installed_manifest_files':len(data['files']),'uninstall_preserves_user_files':True,'real_windows_cpu_tts':True,'real_rtx2070_tested':False,'all_optional_engines_bundled':False,'sources':ASSETS,'packages':[{'name':p.name,'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(OUT.glob('*')) if p.suffix in ('.exe','.zip')]}
 (OUT/'FRESH_INSTALL_VALIDATION.json').write_text(json.dumps(result,indent=2))
 (OUT/'SHA256SUMS_FRESH.txt').write_text(''.join(sha(p)+'  '+p.name+'\n' for p in sorted(OUT.iterdir()) if p.is_file() and p.name!='SHA256SUMS_FRESH.txt'))
 shutil.copytree(WORK/'staging-proof',OUT/'staging-proof',dirs_exist_ok=True);shutil.copytree(WORK/'installed-proof',OUT/'installed-proof',dirs_exist_ok=True)
 print(json.dumps(result,indent=2),flush=True)

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('action',choices=['prepare','finish','verify-install'])
 action=parser.parse_args().action
 {'prepare':prepare,'finish':finish,'verify-install':verify_install}[action]()
