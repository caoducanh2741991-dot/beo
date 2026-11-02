from pathlib import Path
import sys,json,subprocess,os
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parent))
from vi_worker import environment
home=Path('validation-2.0/live-cache Tiếng Việt').resolve();environment(home,offline=False)
from huggingface_hub import snapshot_download
spec=json.load(open(Path(__file__).with_name('sdk_lock.json')));repo='pnnbao-ump/VieNeu-TTS-v3-Turbo';revision=spec['model_revision']
err=OSError(1314,'A required privilege is not held by the client');err.winerror=1314
with patch('os.symlink',side_effect=err) as denied:
 folder=Path(snapshot_download(repo,revision=revision,allow_patterns=['config.json','onnx_update/config.json'],max_workers=2))
 assert denied.call_count==0
 for name in ['config.json','onnx_update/config.json']:
  p=folder/name;assert p.is_file() and not p.is_symlink();assert json.loads(p.read_text())
 print('PASS live official HF snapshot downloaded into Unicode path with symlink privilege denied; copies and blobs retained.',flush=True)
code="from pathlib import Path;import sys;sys.path.insert(0,sys.argv[1]);from vi_worker import environment;environment(sys.argv[2]);from huggingface_hub import snapshot_download;p=Path(snapshot_download(sys.argv[3],revision=sys.argv[4],local_files_only=True));assert (p/'config.json').is_file();print('PASS offline snapshot read with socket/DNS blocked')"
r=subprocess.run([sys.executable,'-I','-c',code,str(Path(__file__).resolve().parent),str(home),repo,revision],capture_output=True,text=True,timeout=20)
assert r.returncode==0,r.stdout+r.stderr;print(r.stdout)
