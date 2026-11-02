"""Run the actual probe subprocess against a deterministic Torch hardware fixture."""
import base64, os, pathlib, shutil, subprocess, sys, tempfile
ROOT=pathlib.Path(__file__).resolve().parent
def main():
 with tempfile.TemporaryDirectory(prefix='v15-probe-') as tmp:
  d=pathlib.Path(tmp)
  shutil.copyfile(ROOT/'ai_worker.py',d/'worker.py');shutil.copyfile(ROOT/'gpu_policy.py',d/'gpu_policy.py')
  (d/'numpy.py').write_text('')
  (d/'soundfile.py').write_text('')
  (d/'torch.py').write_text('''import os
class Cuda:
 def is_available(self): return os.getenv("FAKE_GPU")=="1"
 def get_device_capability(self,i): return (7,5)
 def get_arch_list(self): return ["sm_75"]
 def mem_get_info(self,i): return (6*2**30,8*2**30)
 def get_device_name(self,i): return "RTX 2070 TEST FIXTURE"
 def synchronize(self): pass
cuda=Cuda()
class Tensor:
 def sum(self): return self
 def item(self): return 8
def ones(*a,**kw): return Tensor()
''')
  cases=[('gpu','1',True,'RTX 2070'),('auto','0',True,'CPU dự phòng'),('gpu','0',False,'Không thể dùng'),('cpu','1',True,'đã chọn thủ công')]
  for mode,gpu,ok,expected in cases:
   r=subprocess.run([sys.executable,str(d/'worker.py'),'--probe','--device',mode],env=dict(os.environ,FAKE_GPU=gpu),capture_output=True,text=True,timeout=15)
   lines=[s for s in r.stdout.splitlines() if s.startswith('V15_')]
   assert len(lines)==1,(r.stdout,r.stderr)
   kind,value=lines[0].split('\t');decoded=base64.b64decode(value).decode()
   assert kind==('V15_PROBE' if ok else 'V15_ERR') and expected in decoded,(kind,decoded)
   assert not any(s.startswith('V15_') for s in r.stderr.splitlines())
   assert (r.returncode==0)==ok
   print('PASS',mode,gpu,'UTF-8 protocol is on stdout; decoded device/error matches')
 print('TOTAL WORKER PROBE 4 passed')
if __name__=='__main__':main()
