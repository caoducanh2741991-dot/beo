"""One-time ASR install. Never modifies the existing voice Python/GPU packages."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path
from asr_worker import progress

BASE=Path(__file__).resolve().parent
REQ=['setuptools==75.8.2','faster-whisper==1.1.1','ctranslate2==4.6.0','numpy==2.1.3','pandas==2.2.3','nltk==3.9.1','transformers==4.48.3','scipy==1.15.2']


def digest(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()


def download(url,path,sha256=None):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    if path.is_file() and (not sha256 or digest(path)==sha256):return
    partial=path.with_name(path.name+'.download')
    for attempt in range(3):
        try:
            with urllib.request.urlopen(url,timeout=60) as src,partial.open('wb') as dst:
                length=int(src.headers.get('Content-Length',0));count=0;last=-1
                while True:
                    b=src.read(1024*1024)
                    if not b:break
                    dst.write(b);count+=len(b);percent=int(count*100/length) if length else 0
                    if percent//10!=last:last=percent//10;progress(5,f'Tải {path.name}: {count//1048576} MB'+(f' / {length//1048576} MB' if length else ''))
            if sha256 and digest(partial)!=sha256:raise IOError('Sai mã SHA-256: '+path.name)
            partial.replace(path);return
        except Exception:
            if attempt==2:raise


def call(args):
    env=dict(os.environ,PYTHONNOUSERSITE='1',PIP_DISABLE_PIP_VERSION_CHECK='1');env.pop('HF_HUB_OFFLINE',None);env.pop('TRANSFORMERS_OFFLINE',None)
    p=subprocess.Popen(args,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace',env=env,creationflags=0x08000000 if os.name=='nt' else 0);tail=[]
    for line in p.stdout:
        tail.append(line.strip());tail=tail[-25:]
        if line.strip():progress(10,line.strip()[:240])
    if p.wait():raise RuntimeError('Không cài được bộ phụ đề. Có thể chạy lại để tiếp tục.\n'+'\n'.join(tail))


def main():
    p=argparse.ArgumentParser();p.add_argument('--home',required=True);p.add_argument('--model',choices=['large-v3','distil-large-v3'],default='large-v3');args=p.parse_args();home=Path(args.home);home.mkdir(parents=True,exist_ok=True)
    if os.name!='nt':raise RuntimeError('Trình cài này dành cho Windows x64.')
    existing=sum(f.stat().st_size for f in home.rglob('*') if f.is_file());needed=max(2*1024**3,12*1024**3-existing)
    if shutil.disk_usage(home).free<needed:raise RuntimeError('Cần khoảng '+str((needed+1024**3-1)//1024**3)+' GB trống để tiếp tục cài bộ phụ đề.')
    os.environ['PIP_CACHE_DIR']=str(home/'downloads/pip-cache');lock=json.loads((BASE/'download_lock.json').read_text())
    progress(1,'Cài bộ phụ đề riêng. Lần đầu cần mạng và tải vài GB; sau đó chạy offline.')
    runtime=home/'runtime';python=runtime/'python.exe';archive=home/'downloads/python-embed.zip';download(lock['python']['url'],archive,lock['python']['sha256']);runtime.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive) as z:z.extractall(runtime)
    (runtime/'Lib/site-packages').mkdir(parents=True,exist_ok=True);(runtime/'python312._pth').write_text('python312.zip\n.\nLib\nLib/site-packages\n'+str(BASE)+'\nimport site\n',encoding='utf-8')
    pip=home/'downloads/pip.whl';download(lock['pip']['url'],pip,lock['pip']['sha256'])
    with zipfile.ZipFile(pip) as z:z.extractall(runtime/'Lib/site-packages')
    call([str(python),'-m','pip','install','--upgrade','--only-binary=:all:','torch==2.6.0+cu124','torchaudio==2.6.0+cu124','--index-url','https://download.pytorch.org/whl/cu124','--extra-index-url','https://pypi.org/simple'])
    call([str(python),'-m','pip','install','--upgrade','--only-binary=:all:']+REQ)
    call([str(python),'-m','pip','install','--no-deps','whisperx==3.3.4'])
    model=lock[args.model];progress(30,'Tải mô hình '+args.model+' và mô hình căn thời gian…')
    code="from huggingface_hub import snapshot_download;import sys;snapshot_download(sys.argv[1],revision=sys.argv[2],local_dir=sys.argv[3],allow_patterns=['model.bin','config.json','tokenizer.json','preprocessor_config.json','vocabulary.*'])"
    call([str(python),'-c',code,model['repo'],model['revision'],str(home/'models'/args.model)])
    align=home/'models/alignment/wav2vec2_fairseq_base_ls960_asr_ls960.pth';download('https://download.pytorch.org/torchaudio/models/'+align.name,align,'488fd4f16de84438ffc945334278c1b9fb9b7159a806c1080b16111a958c945d')
    progress(90,'Kiểm tra các thư viện và dữ liệu offline…');call([str(python),str(BASE/'asr_worker.py'),'probe','--home',str(home),'--model',args.model,'--device','cpu'])
    (home/'ready.json').write_text(json.dumps(dict(version='1.4.10',model=args.model)),encoding='utf-8');progress(100,'Đã cài bộ phụ đề '+args.model+'. Có thể ngắt mạng và tạo SRT.')


if __name__=='__main__':
    try:main()
    except Exception as e:progress(0,str(e));sys.exit(1)
