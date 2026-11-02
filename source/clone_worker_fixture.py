"""Deterministic process fixture for parent lifecycle/cancel/batch recovery tests."""
import argparse,base64,os,pathlib,struct,sys,time,wave,math
p=argparse.ArgumentParser();p.add_argument('--device');p.add_argument('--seed-offset',default='0');a,_=p.parse_known_args()
root=pathlib.Path(__file__).resolve().parent
scenario=(root/'scenario.txt').read_text().strip()
with (root/'starts.txt').open('a') as f:f.write(a.device+' '+a.seed_offset+' '+str(os.getpid())+'\n')
with (root/'allpids.txt').open('a') as f:f.write(str(os.getpid())+'\n')
def emit(k,s=''):print('V15_'+k+'\t'+base64.b64encode(s.encode()).decode(),flush=True)
def once(name):
 f=root/name
 if f.exists():return False
 f.touch();return True
if scenario=='startupoom' and a.device!='cpu':emit('ERR','GPU_FALLBACK: CUDA startup failure');sys.exit(1)
if scenario=='startuphangalways':time.sleep(10)
emit('STATUS','Đang nạp mẫu fixture')
emit('READY','CPU fixture' if a.device=='cpu' else 'NVIDIA CUDA fixture')
requests=0
for line in sys.stdin:
 fields=line.rstrip().split('\t')
 if fields==['QUIT']:break
 requests+=1;output=base64.b64decode(fields[2]).decode()
 emit('STATUS','Sinh fixture · thử 1/3')
 if scenario in ['hangonce','hangalways','cancel','statushang'] and (scenario!='hangonce' or once('hung')):
  if scenario=='statushang':
   for i in range(200):emit('STATUS','Vẫn chạy '+str(i));time.sleep(.04)
  else:time.sleep(10)
 if scenario=='crashonce' and once('crashed'):os._exit(9)
 if scenario=='oom' and a.device!='cpu':emit('ERR','GPU_FALLBACK: CUDA out of memory');continue
 if scenario=='unrelated':emit('ERR','missing reference');continue
 if scenario=='invalid' or (scenario=='cue10' and requests==10):emit('ERR','SYNTH_INVALID: fixture exhausted 3 generation attempts');continue
 if scenario=='malformed':print('V15_ERR\t%%%INVALID%%%',flush=True);continue
 if scenario=='badwav':pathlib.Path(output).write_bytes(b'not wave');emit('OK','NVIDIA CUDA fixture');continue
 with wave.open(output,'wb') as w:
  w.setparams((1,2,24000,0,'NONE','not compressed'))
  w.writeframes(b''.join(struct.pack('<h',int(6000*math.cos(i*.0576))) for i in range(9600)))
 emit('OK','CPU fixture' if a.device=='cpu' else 'NVIDIA CUDA fixture')
