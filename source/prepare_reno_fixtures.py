"""Extract a reproducible text/measurement replay from user-supplied 2.0.17 reports.
These are NOT recordings or new TTS runs. Source SRT bytes are unavailable:
reconstructed SRTs preserve only the reported English/cue IDs/timecodes.
"""
from pathlib import Path
import re,json,hashlib,sys
SOURCE=Path(__file__).resolve().parent
DEST=SOURCE/'fixtures/reno217'
def stamp(s):
 h,m,t=s.replace(',','.').split(':');return int(h)*3600+int(m)*60+float(t)
def measurement(s):
 m=re.search(r'TTS gồm nghỉ tự nhiên ([\d.]+)s; nghỉ app ([\d.]+)s; hơi thở ([\d.]+)s; tổng ([\d.]+)s; sau căn ([\d.]+)s; đích ([\d.]+)s; chênh ([-\d.]+)s; tốc độ ([\d.]+)x',s)
 return dict(zip(['tts','pauses','breaths','natural','after','target','delta','speed'],map(float,m.groups()))) if m else None

def main(folder):
 DEST.mkdir(parents=True,exist_ok=True);audit={'provenance':'Replay extracted from actual user 2.0.17 reports. No voice audio is included. Reconstructed SRT is not an original-byte copy.','cases':[]}
 for n in (1,2,3):
  sources=[folder/f'reno {n} (2).voice-report.txt',folder/f'reno {n}.ai-before-after-report.txt',folder/f'reno {n}.ai-changes-en-vi.txt']
  file,auto,short=[p.read_text() for p in sources]
  cues=[{'id':int(i),'start':stamp(a),'end':stamp(b),'text':t} for i,a,b,t in re.findall(r'^#(\d+) \| (\d\d:\d\d:\d\d,\d{3}) --> (\d\d:\d\d:\d\d,\d{3}) \| (.*)$',file,re.M)]
  assert len(cues)=={1:48,2:68,3:76}[n]
  def file_stages():
   result=[]
   for b in re.split(r'(?m)^Chặng (?=\d+ \| cue)',file)[1:]:
    head=re.match(r'(\d+) \| cue ([\d,]+) \| nguồn (\S+) → (\S+) \| khung ([\d.]+)s',b)
    if not head:continue
    before=re.search(r'trước căn \(gồm nghỉ/hơi thở ghép\): ([\d.]+)s \| sau căn: ([\d.]+)s',b)
    comp=re.search(r'audio TTS gốc \(gồm nghỉ tự nhiên\) ([\d.]+)s; nghỉ app trong đơn vị ([\d.]+)s; nghỉ app giữa đơn vị \(không gồm hơi thở\) ([\d.]+)s; hơi thở ([\d.]+)s',b)
    speed=re.search(r'Tốc độ chặng: ([\d.]+)x',b)
    if not before or not comp or not speed:continue
    natural,after=map(float,before.groups());tts,inner,between,breaths=map(float,comp.groups());target=float(head[5]);result.append({'number':int(head[1]),'ids':list(map(int,head[2].split(','))),'measure':dict(tts=tts,pauses=inner+between,breaths=breaths,natural=natural,after=after,target=target,delta=round(after-target,3),speed=float(speed[1])),'attempts':[]})
   return result
  astages=[]
  for b in re.split(r'(?m)^CHẶNG (?=\d+ \| cue)',auto)[1:]:
   h=re.match(r'(\d+) \| cue ([\d,]+) \| (\S+) --> (\S+)',b);assert h
   trials=[]
   for a in re.split(r'(?m)^Thử ',b)[1:]:
    ah=re.match(r'(\d+) \| ([^|]+) \| ([^|]+) \| (.*)',a);assert ah
    trials.append({'number':int(ah[1]),'strategy':ah[2].strip(),'code':ah[3].strip(),'cues':[{'id':int(i),'text':t} for i,t in re.findall(r'^Đề xuất #(\d+): (.*)$',a,re.M)],'measure':measurement(a),'model_verdict':'equivalent' if 'Model review: equivalent;' in a else ''})
   astages.append({'number':int(h[1]),'ids':list(map(int,h[2].split(','))),'measure':measurement(b.split('SAU/ĐANG DÙNG:')[0]),'attempts':trials})
  fs=file_stages();assert len(fs)==len(astages)=={1:12,2:17,3:19}[n]
  audit['cases'].append({'name':f'reno {n}','cues':cues,'file_stages':fs,'auto_stages':astages,'auto_status':'CANCELLED' if n==3 else 'NEEDS_REVIEW','reports':[{'name':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]})
  lines=[]
  for i,a,b,t in re.findall(r'^#(\d+) \| (\d\d:\d\d:\d\d,\d{3}) --> (\d\d:\d\d:\d\d,\d{3}) \| (.*)$',file,re.M):lines += [i,a+' --> '+b,t,'']
  (DEST/f'reno-{n}-reconstructed.srt').write_text('\n'.join(lines))
 (DEST/'audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2))
 print('Extracted',[(c['name'],len(c['cues']),len(c['file_stages']),sum(x['measure'] is not None for x in c['auto_stages'])) for c in audit['cases']])
if __name__=='__main__':main(Path(sys.argv[1]))
