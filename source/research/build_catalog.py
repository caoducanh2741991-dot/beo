from pathlib import Path
from collections import Counter
import json,re,csv
base=Path('V15VoiceStudio');r=base/'research';target=base/'package';target.mkdir(exist_ok=True)
metrics={x['key']:x for x in json.loads((r/'voice_metrics.json').read_text())}
annotations=[]
for n in [1,2,3]:
 d={}
 for line in (r/f'df{n}_en.csv').read_text().splitlines():
  a,b=line.split('|',1);d[a]=b
 annotations.append(d)
mapping=json.load(open('/tmp/v15-build/package/libritts/en_US-libritts_r-medium.onnx.json'))['speaker_id_map'];reverse={v:k for k,v in mapping.items()}
names=['Alloy','Aoede','Bella','Heart','Jessica','Kore','Nicole','Nova','River','Sarah','Sky','Adam','Echo','Eric','Fenrir','Liam','Michael','Onyx','Puck','Santa']
grades={}
for line in (r/'KOKORO_VOICES.md').read_text().splitlines():
 cols=[c.strip().replace('**','').replace('\\_','_') for c in line.split('|')]
 if len(cols)>6 and re.match(r'a[fm]_',cols[1]):grades[cols[1]]=cols[5]
translate={'soft':'mềm mại','clear':'rõ tiếng','raspy':'khàn','bright':'sáng','muffled':'hơi đục','nasal':'âm mũi','thick':'dày','thin':'mỏng','dark':'tối','fluent':'mượt','halting':'ngập ngừng','lively':'sinh động','calm':'điềm tĩnh','powerful':'có lực','relaxed':'thả lỏng','tensed':'hơi căng','hard':'cứng','sharp':'sắc','sweet':'ngọt dịu','friendly':'thân thiện','reassuring':'trấn an','sincere':'chân thành','elegant':'trang nhã','strict':'nghiêm','light':'nhẹ','weak':'nhẹ lực'}
def aggregate(original):
 scores=Counter();available=False
 for d in annotations:
  if original not in d:continue
  available=True
  for tag in d[original].split(','):
   weight=1.0
   if tag.startswith('very '):tag=tag[5:];weight=1.5
   if tag.startswith('slightly '):tag=tag[9:];weight=.5
   scores[tag]+=weight
 if not available:return 'Chưa rõ','Chưa có mô tả giọng nguồn đáng tin cho speaker này.'
 male=scores['masculine'];female=scores['feminine'];gender='Nam (nguồn)' if male>female+1 else 'Nữ (nguồn)' if female>male+1 else 'Trung tính (nguồn)'
 traits=[]
 for k,v in scores.most_common():
  if k not in translate or v<1:continue
  if any((k in pair and any(t in pair for t in traits)) for pair in [('soft','hard'),('bright','dark'),('clear','muffled'),('fluent','halting'),('thick','thin'),('relaxed','tensed')]):continue
  traits.append(k)
  if len(traits)==5:break
 return gender,'Giọng nguồn: '+', '.join(translate[k] for k in traits)+'.'
def measured(m):
 pitch='tông chưa đo ổn định' if m['hz']==0 else 'tông '+('trầm' if m['hz']<140 else 'trung' if m['hz']<200 else 'cao')+f" (~{m['hz']} Hz)"
 pace='chậm hơn' if m['wpm']<185 else 'vừa' if m['wpm']<220 else 'nhanh hơn'
 variation='nhẹ' if m['spread']<3 else 'vừa' if m['spread']<6 else 'rộng'
 return f"Mẫu TTS: {pitch}; nhịp {pace} (~{m['wpm']} từ/phút); biến thiên tông {variation}."
profiles=[]
for lib in [False,True]:
 for i in range(904 if lib else 20):
  key=('L:' if lib else 'K:')+str(i);m=metrics[key]
  if lib:
   orig=reverse[i];gender,traits=aggregate(orig);desc=traits+' '+measured(m);grade='Chưa có';star=0
   source=f'LibriTTS-P/LINE, tổng hợp 3 người chú thích, speaker gốc {orig}; CC BY 4.0; https://github.com/line/LibriTTS-P . Đặc điểm nguồn không bảo đảm khớp TTS; accent chưa xác minh.'
  else:
   gender='Nữ' if i<11 else 'Nam';grade=grades[('af_' if i<11 else 'am_')+names[i].lower()];star=int(grade in ['A','A-','B+','B','B-']);desc=measured(m)+' American English theo model.'
   source=f'Kokoro VOICES.md: Overall Grade {grade}, ước lượng chất lượng/lượng dữ liệu huấn luyện, không phải điểm cộng đồng; https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md'
  source+=' Số đo ước tính từ cùng lời mẫu 18 từ, speed=1.00; không phải chứng nhận chất lượng.'
  profiles.append([key,gender,desc,source,grade,str(star)])
with (target/'voice_profiles.tsv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f,delimiter='\t',lineterminator='\n',quoting=csv.QUOTE_NONE,escapechar='\\');w.writerow(['Key','Gender','Description','Source','Grade','Recommended']);w.writerows(profiles)
with (target/'voices.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['Key','Loai giong','Dac diem','Nguon','Xep hang','Danh dau sao']);w.writerows(profiles)
print('Profiles',len(profiles),'stars',[(p[0],p[4]) for p in profiles if p[5]=='1'])
