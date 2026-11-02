from pathlib import Path
from collections import Counter
import json,re,wave,time,csv
import numpy as np
from scipy.signal import fftconvolve,find_peaks
import sherpa_onnx
root=Path('/tmp/v15-build/package');out=Path('/tmp/v15-profiles');out.mkdir(exist_ok=True)
text='Have you ever forgotten why you entered a room? Your brain may have switched contexts at the doorway.'
words=len(re.findall(r"[A-Za-z]+",text))
def metrics(x,sr):
 x=np.asarray(x,dtype=np.float64);n=int(sr*.04);hop=int(sr*.02);f0=[]
 for i in range(0,len(x)-n,hop):
  a=x[i:i+n];a=a-a.mean()
  if np.sqrt(np.mean(a*a))<.006:continue
  a=a*np.hanning(n);acf=fftconvolve(a,a[::-1],mode='full')[n-1:];acf=acf/(acf[0]+1e-12)
  low=int(sr/450);high=min(len(acf)-1,int(sr/65));peaks,_=find_peaks(acf[low:high])
  if len(peaks)==0:continue
  lag=int(peaks[np.argmax(acf[low+peaks])]+low)
  if acf[lag]>.45:f0.append(sr/lag)
 dur=len(x)/sr;f=float(np.median(f0)) if len(f0)>5 else 0
 spread=float(12*np.log2(np.percentile(f0,80)/np.percentile(f0,20))) if f>0 else 0
 return {'seconds':round(dur,3),'wpm':round(words*60/dur),'hz':round(f),'spread':round(spread,1)}
cfg=sherpa_onnx.OfflineTtsConfig(model=sherpa_onnx.OfflineTtsModelConfig(vits=sherpa_onnx.OfflineTtsVitsModelConfig(model=str(root/'libritts/en_US-libritts_r-medium.onnx'),tokens=str(root/'libritts/tokens.txt'),data_dir=str(root/'libritts/espeak-ng-data')),num_threads=2,provider='cpu'),max_num_sentences=2)
m=sherpa_onnx.OfflineTts(cfg);results=[]
for sid in range(904):
 p=out/f'L{sid}.json'
 if p.exists():result=json.loads(p.read_text())
 else:
  audio=m.generate(text,sid=sid,speed=1.0);result={'key':f'L:{sid}',**metrics(audio.samples,audio.sample_rate)};p.write_text(json.dumps(result))
 results.append(result)
 if sid%100==0:print('Profiles',sid,'/904',flush=True)
for sid,name in enumerate(['Alloy','Aoede','Bella','Heart','Jessica','Kore','Nicole','Nova','River','Sarah','Sky','Adam','Echo','Eric','Fenrir','Liam','Michael','Onyx','Puck','Santa']):
 with wave.open(str(root/'samples'/f'{sid:02d}_{name}_US.wav')) as w:x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2')/32768.;sr=w.getframerate()
 results.append({'key':f'K:{sid}',**metrics(x,sr)})
Path('V15VoiceStudio/research/voice_metrics.json').write_text(json.dumps(results,indent=2));print('DONE',len(results),flush=True)
