"""Rebuild historical fixtures from the six original reports placed in ../upload. No voice/GPU test is performed."""
from pathlib import Path
import re,json,hashlib
ROOT=Path(__file__).resolve().parent.parent
UPLOAD=ROOT.parent/'upload';OUT=ROOT/'source/fixtures/van215'
def number(p,s):
 m=re.search(p,s);return float(m.group(1)) if m else None
def cues(text):
 rows=[]
 for block in re.split(r'\n\s*\n',text.strip()):
  lines=block.splitlines()
  if len(lines)>=3 and '-->' in lines[1]:rows.append({'id':int(lines[0]),'timecode':lines[1],'text':' '.join(lines[2:])})
 return rows
allcases=[]
for n in [4,5,6]:
 ai=(UPLOAD/f'van {n}.ai-repair-report(1).txt').read_text(encoding='utf-8-sig');voice=(UPLOAD/f'van {n}.voice-report(1).txt').read_text(encoding='utf-8-sig')
 source=OUT/f'van {n}.srt';rows=cues(source.read_text(encoding='utf-8-sig'));digest=hashlib.sha256(source.read_bytes()).hexdigest();assert digest in ai.lower() and digest in voice.lower()
 sourceblock=voice.split('CUE NGUỒN ·')[1].split('ĐƠN VỊ LỜI ĐỌC ·')[0]
 rendered={int(i):t for i,t in re.findall(r'^#(\d+) \|[^\n]*\nGiọng câu:[^\n]*\nLời: ([^\n]+)',sourceblock,re.M)}
 stages=[];finalblock=voice.split('CHẶNG V15.6 ·')[1].split('LỊCH SỬ SỬA')[0]
 for block in re.split(r'(?=Đơn vị \d+ · cue)',ai)[1:]:
  num=int(re.match(r'Đơn vị (\d+)',block).group(1));ids=[int(x) for x in re.search(r'· cue ([\d,]+)',block).group(1).split(',')]
  original={int(i):t for i,t in re.findall(r'^Gốc #(\d+): ([^\n]+)',block,re.M)}
  assert all(original[c['id']]==c['text'] for c in rows if c['id'] in ids)
  probe=block.split('Đo gốc:',1)[1].split('\n',1)[0]
  final=re.search(r'Chặng '+str(num)+r' \| cue.*?(?=Chặng \d+ \| cue|\Z)',finalblock,re.S).group(0)
  attempts=[]
  for a in re.split(r'(?=Lần AI \d+)',block)[1:]:
   attempts.append({'number':int(re.match(r'Lần AI (\d+)',a).group(1)),'accepted':'ĐÃ CHẤP NHẬN' in a.splitlines()[0],'cues':[{'id':int(i),'text':t} for i,t in re.findall(r'^Sửa #(\d+): ([^\n]+)',a,re.M)],'natural':number(r'TTS và nghỉ trước căn ([\d.]+)s',a)})
  stages.append({'number':num,'ids':ids,'probe_natural':number(r'TTS và nghỉ trước căn ([\d.]+)s',probe),'probe_after':number(r'sau căn đo ([\d.]+)s',probe),'target':number(r'đích thử ([\d.]+)s',probe),'final_natural':number(r'trước căn \(gồm nghỉ/hơi thở ghép\): ([\d.]+)s',final),'final_after':number(r'sau căn: ([\d.]+)s',final),'final_speed':number(r'Tốc độ chặng: ([\d.]+)x',final),'final_tts':number(r'audio TTS gốc \(gồm nghỉ tự nhiên\) ([\d.]+)s',final),'attempts':attempts})
 case={'name':f'van {n}','sha256':digest,'cues':rows,'stages':stages,'old_wav_changed_ids':[c['id'] for c in rows if rendered.get(c['id'])!=c['text']],'old_wav_success':'Kết quả: THÀNH CÔNG' in voice,'evidence':'Attached 2.0.14 reports; these are historical measurements, not new TTS/GPU runs.'}
 allcases.append(case)
 print(case['name'],'cues',len(rows),'stages',len(stages),'old changed IDs',case['old_wav_changed_ids'])
(OUT/'audit.json').write_text(json.dumps({"cases":allcases},ensure_ascii=False,indent=2))
