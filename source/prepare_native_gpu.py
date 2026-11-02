"""Reproducible Windows x64 CUDA dependency pack. Does not modify installed runtimes."""
from pathlib import Path
import concurrent.futures as futures
import hashlib, json, shutil, struct, subprocess, tarfile, time, urllib.request, zipfile

ROOT = Path(__file__).resolve().parent.parent
CACHE = Path('/tmp/v15-native-downloads')
DEST = Path('/tmp/v15-native-gpu')
SEVEN = '/tmp/v15-tools/usr/lib/7zip/7z'
VERSIONS = {'nvidia-cublas-cu12':'12.8.4.1','nvidia-cuda-runtime-cu12':'12.8.90',
 'nvidia-cufft-cu12':'11.3.3.83','nvidia-cudnn-cu12':'9.8.0.87',
 'nvidia-cuda-nvrtc-cu12':'12.8.93','nvidia-nvjitlink-cu12':'12.8.93'}
SOURCES = []

def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for b in iter(lambda:f.read(4*1024*1024),b''): h.update(b)
 return h.hexdigest()

def download(url, name, expected=None):
 target=CACHE/name
 if target.exists() and (not expected or sha(target)==expected): return target
 for attempt in range(4):
  try:
   request=urllib.request.Request(url,headers={'User-Agent':'V15VoiceStudio-build/1.4.3'})
   with urllib.request.urlopen(request,timeout=90) as src, target.with_suffix(target.suffix+'.tmp').open('wb') as dst:
    shutil.copyfileobj(src,dst,4*1024*1024)
   target.with_suffix(target.suffix+'.tmp').replace(target)
   if expected and sha(target)!=expected: raise ValueError('SHA256 mismatch '+name)
   print('Downloaded and verified',name,target.stat().st_size,flush=True)
   return target
  except Exception:
   if attempt==3: raise
   time.sleep(2+attempt)

def wheel(name, version):
 metadata=json.loads(urllib.request.urlopen(f'https://pypi.org/pypi/{name}/{version}/json',timeout=60).read())
 asset=next(x for x in metadata['urls'] if x['filename'].endswith('win_amd64.whl'))
 f=download(asset['url'],asset['filename'],asset['digests']['sha256'])
 with zipfile.ZipFile(f) as z:
  for e in z.infolist():
   base=Path(e.filename).name
   if base.lower().endswith('.dll') and not base.lower().startswith(('nvblas','cufftw')):
    with z.open(e) as src,(DEST/base).open('wb') as dst: shutil.copyfileobj(src,dst)
   elif 'license' in base.lower() and not e.is_dir():
    (DEST/('LICENSE-'+name+'.txt')).write_bytes(z.read(e))
 return {'name':name,'version':version,'url':asset['url'],'sha256':sha(f)}

def sherpa():
 name='sherpa-onnx-v1.13.8-cuda-12.x-cudnn-9.x-onnxruntime1.28.2-win-x64-cuda.tar.bz2'
 url='https://github.com/k2-fsa/sherpa-onnx/releases/download/v1.13.8/'+name
 f=download(url,name,'066c5b54dbafaa1388001a9c9837ac1374dbba6d6678f193ca06aa0d8e94d8c3')
 allowed={'sherpa-onnx-offline-tts.exe','onnxruntime.dll','onnxruntime_providers_cuda.dll','onnxruntime_providers_shared.dll'}
 with tarfile.open(f,'r:bz2') as t:
  for e in t:
   if Path(e.name).name in allowed:
    with t.extractfile(e) as src,(DEST/Path(e.name).name).open('wb') as dst: shutil.copyfileobj(src,dst)
 for repo,tag,label in [('k2-fsa/sherpa-onnx','v1.13.8','sherpa-onnx'),('microsoft/onnxruntime','v1.28.2','onnxruntime')]:
  lic=download(f'https://raw.githubusercontent.com/{repo}/{tag}/LICENSE',label+'-LICENSE.txt')
  shutil.copyfile(lic,DEST/('LICENSE-'+label+'.txt'))
 return {'name':'sherpa-onnx','version':'1.13.8','onnxruntime':'1.28.2','url':url,'sha256':sha(f)}

def vc_runtime():
 f=download('https://aka.ms/vs/17/release/vc_redist.x64.exe','vc_redist.x64.exe')
 data=f.read_bytes();out=CACHE/'vc';out.mkdir(exist_ok=True)
 cabinets=[];start=0
 while True:
  pos=data.find(b'MSCF\0\0\0\0',start)
  if pos<0:break
  size=struct.unpack_from('<I',data,pos+8)[0];start=pos+8
  if 36<=size<=len(data)-pos:
   cab=out/f'bundle-{len(cabinets)}.cab';cab.write_bytes(data[pos:pos+size]);cabinets.append(cab)
 extracted=[]
 for i,cab in enumerate(cabinets):
  d=out/str(i);subprocess.run([SEVEN,'x',str(cab),'-o'+str(d),'-y'],check=True,stdout=subprocess.DEVNULL);extracted.append(d)
 from xml.etree import ElementTree as ET
 manifest=ET.fromstring((extracted[0]/'0').read_bytes())
 payloads={e.attrib.get('FilePath'):e.attrib.get('SourcePath') for e in manifest.iter() if e.tag.split('}')[-1]=='Payload'}
 cab_name=next(value for key,value in payloads.items() if 'vcruntimeminimum_amd64' in key.lower() and key.lower().endswith('.cab'))
 minimum=out/'minimum';subprocess.run([SEVEN,'x',str(extracted[1]/cab_name),'-o'+str(minimum),'-y'],check=True,stdout=subprocess.DEVNULL)
 count=0
 for p in minimum.rglob('*'):
  if p.is_file() and p.name.lower().endswith('.dll_amd64'):
   shutil.copyfile(p,DEST/p.name[:-6]);count+=1
 assert count>=8,count
 license_path=payloads.get('license.rtf')
 if license_path and (extracted[0]/license_path).exists(): lic=extracted[0]/license_path
 else:
  lic=next(p for p in extracted[0].iterdir() if p.is_file() and p.read_bytes()[:6]==b'{\\rtf1')
 shutil.copyfile(lic,DEST/'LICENSE-Microsoft-Visual-Cpp.rtf')
 return {'name':'Microsoft Visual C++ Redistributable x64','url':'https://aka.ms/vs/17/release/vc_redist.x64.exe','sha256':sha(f)}

def main():
 CACHE.mkdir(exist_ok=True);DEST.mkdir(exist_ok=True)
 with futures.ThreadPoolExecutor(max_workers=4) as pool:
  tasks=[pool.submit(wheel,n,v) for n,v in VERSIONS.items()]+[pool.submit(sherpa),pool.submit(vc_runtime)]
  for future in futures.as_completed(tasks): SOURCES.append(future.result())
 for file in DEST.iterdir():
  if file.suffix.lower() in ('.dll','.exe'):
   with file.open('rb') as f:
    assert f.read(2)==b'MZ',file
    f.seek(60);offset=struct.unpack('<I',f.read(4))[0];f.seek(offset)
    assert f.read(4)==b'PE\0\0' and struct.unpack('<H',f.read(2))[0]==0x8664,file
 info={'product':'V15 native ONNX CUDA pack','app_minimum':'1.4.3','hardware_tested':False,
  'cuda':'12.8 Update 1','cudnn':'9.8.0.87','recommended_windows_driver_minimum':'572.61',
  'sources':sorted(SOURCES,key=lambda x:x['name']),
  'files':[{ 'name':p.name,'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(DEST.iterdir()) if p.is_file()]}
 (DEST/'info.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8')
 print('READY',len(list(DEST.iterdir())),sum(p.stat().st_size for p in DEST.iterdir()),flush=True)

if __name__=='__main__':main()
