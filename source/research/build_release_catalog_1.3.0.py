from pathlib import Path
import csv,json,re,shutil,collections
base=Path('/tmp/v15-build/package');r=base/'source/research';q=Path('/tmp/v15-130-research')
old=base/'voice_profiles.tsv';archive=r/'voice_profiles_all_1.2.0.tsv'
if not archive.exists():shutil.copyfile(old,archive)
profiles={p['Key']:p for p in csv.DictReader(archive.open(encoding='utf-8-sig'),delimiter='\t')}
metrics={m['key']:m for m in json.loads((r/'voice_metrics.json').read_text())}
reverse={v:k for k,v in json.load((base/'libritts/en_US-libritts_r-medium.onnx.json').open())['speaker_id_map'].items()}
annotations=[dict(line.split('|',1) for line in (r/f'df{n}_en.csv').read_text().splitlines())for n in [1,2,3]]
def traits(key):
 d=collections.Counter()
 if not key.startswith('L:'):return d
 for a in annotations:
  for tag in a.get(reverse[int(key[2:])],'').split(','):
   weight=.5 if tag.startswith('slightly ') else 1.5 if tag.startswith('very ') else 1
   d[re.sub(r'^(slightly |very )','',tag)]+=weight
 return d
review='Review';story='Kể chuyện';mystery='Bí ẩn / Dark curiosity';facts='Psychology Facts / V15';guide='Giải thích / Hướng dẫn';ads='Quảng cáo / Hook TikTok';doc='Tài liệu / Thuyết minh'
kokoro={0:[review,guide,facts],1:[story,doc,review],2:[review,ads,facts],3:[facts,review,story],4:[review,guide,facts],5:[facts,review,story],6:[story,mystery,doc],7:[ads,review,facts],8:[story,review,guide],9:[guide,facts,doc],10:[story,review,facts],11:[mystery,story,doc],12:[story,guide,doc],13:[review,facts,guide],14:[ads,story,review],15:[guide,doc,facts],16:[doc,story,guide],17:[mystery,doc,story],18:[ads,review,story],19:[story,mystery,doc]}
def classify(key):
 d=traits(key);m=metrics[key];hz=m['hz'];pace=m['wpm'];spread=m['spread']
 scores={review:d['clear']*2+d['friendly']*2+d['bright']+d['fluent'],story:d['calm']*2+d['soft']+d['thick']+d['fluent']+(1.5 if 3<spread<7 else 0),mystery:d['dark']*2+d['raspy']*1.5+d['cool']+d['calm']+(2 if hz<145 else 0),facts:d['clear']*2+d['intellectual']*2+d['sincere']+d['fluent']+(1 if 185<pace<235 else 0),guide:d['clear']*2+d['calm']+d['sincere']+(1 if spread<5 else 0)+(1 if pace<210 else 0),ads:d['bright']*2+d['powerful']*2+d['lively']*2+(1 if pace>225 else 0)+(1 if spread>5 else 0),doc:d['clear']*2+d['mature']*2+d['calm']+(1 if hz<160 else 0)+(1 if spread<4 else 0)}
 return sorted(scores,key=lambda k:-scores[k])[:3]
keys=json.loads((q/'selected_libri.json').read_text());rows=[]
new_samples=base/'samples/curated';new_samples.mkdir(exist_ok=True)
cols=['Key','Gender','Description','Source','Grade','Recommended','Name','Engine','Speaker','ModelFolder','Styles','StyleReason','Sample','StarSource']
for key in ['K:'+str(i)for i in range(20)]+keys:
 p=dict(profiles[key]);sid=int(key[2:]);lib=key.startswith('L:');p.update(Name='Libri #'+str(sid).zfill(3) if lib else 'Kokoro · '+['Alloy','Aoede','Bella','Heart','Jessica','Kore','Nicole','Nova','River','Sarah','Sky','Adam','Echo','Eric','Fenrir','Liam','Michael','Onyx','Puck','Santa'][sid],Engine='LibriTTS' if lib else 'Kokoro',Speaker=str(sid),ModelFolder='',Styles='|'.join(classify(key)if lib else kokoro[sid]),StyleReason='Gợi ý biên tập theo đặc điểm nguồn và mẫu tổng hợp (tông, nhịp, độ biến thiên); nên nghe thử với kịch bản của bạn.',StarSource='Kokoro: Overall Grade '+p['Grade']+' về dữ liệu huấn luyện, không phải bình chọn cộng đồng.'if not lib and p['Recommended']=='1' else '')
 if lib:
  qa=json.loads((q/(key.replace(':','_')+'_qa.json')).read_text());p['Source']+=' Tuyển chọn V15 1.3: 100 ứng viên sơ tuyển theo mô tả nguồn; giữ 10 nam + 10 nữ qua 2 đoạn kiểm tra nội dung bằng ASR và mức âm thanh. Kiểm tra lại với duration-noise=0 theo bản phát hành. Không phải đánh giá nghe mù, MOS hay chứng nhận accent US.'
  p['Grade']='V15 tuyển chọn';p['Recommended']='1' if key in keys[:3]+keys[10:13] else '0';p['StarSource']='V15 ưu tiên thử: đạt kiểm tra mẫu; không phải xếp hạng cộng đồng.'if p['Recommended']=='1'else''
  name=key.replace(':','_')+'.wav';shutil.copyfile(q/'auditions'/(key.replace(':','_')+'_stable.wav'),new_samples/name);p['Sample']='samples/curated/'+name
 else:p['Sample']='samples/'+str(sid).zfill(2)+'_'+p['Name'].split(' · ')[1]+'_US.wav'
 rows.append(p)
kitten_names=['Jasper','Bella','Bruno','Luna','Hugo','Rosie','Leo','Kiki']
kitten_styles=[[story,doc,guide],[review,facts,ads],[mystery,story,doc],[story,review,facts],[doc,guide,story],[review,guide,facts],[ads,review,story],[ads,facts,review]]
for sid,name in enumerate(kitten_names):
 key='T:'+str(sid);qa=json.loads((q/('T_'+str(sid)+'_qa.json')).read_text())
 if qa['wer']>.10:print('EXCLUDE kitten:',name,qa['wer']);continue
 sample='T_'+str(sid)+'.wav';shutil.copyfile(q/'auditions'/('T_'+str(sid)+'_0.wav'),new_samples/sample)
 rows.append(dict(Key=key,Gender='Nam'if sid%2==0 else'Nữ',Description='Kitten Mini 0.8: preset '+name+'; English, 24 kHz. Accent US từng giọng chưa được chứng nhận.',Source='KittenML/kitten-tts-mini-0.8, Apache 2.0; sherpa-onnx kitten-mini-en-v0_8. Qua 2 đoạn kiểm tra nội dung; không phải điểm tự nhiên hay đánh giá cộng đồng. https://huggingface.co/KittenML/kitten-tts-mini-0.8',Grade='Chưa có',Recommended='0',Name='Kitten · '+name,Engine='Kitten',Speaker=str(sid),ModelFolder='',Styles='|'.join(kitten_styles[sid]),StyleReason='Gợi ý biên tập để thử phong cách; không phải phong cách đã được nhà phát hành chứng nhận. Nghe mẫu trước khi dùng.',Sample='samples/curated/'+sample,StarSource=''))
for name,gender,styles in [('john','Nam',[story,doc,guide]),('kristin','Nữ',[doc,guide,story])]:
 key='P:'+name;qa=json.loads((q/('P_'+name+'_qa.json')).read_text())
 if qa['wer']>.10:print('EXCLUDE Piper',name,qa['wer']);continue
 sample='P_'+name+'.wav';shutil.copyfile(q/'auditions'/('P_'+name+'_stable.wav'),new_samples/sample)
 rows.append(dict(Key=key,Gender=gender,Description='Piper '+name.capitalize()+' medium: giọng US '+('nam'if gender=='Nam'else'nữ')+' đọc sách/thuyết minh; 22.05 kHz.',Source='Piper/rhasspy, en_US-'+name+'-medium; dữ liệu LibriVox public domain, model card ghi giọng US. https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/'+name+'/medium',Grade='Chưa có',Recommended='0',Name='Piper · '+name.capitalize(),Engine='Piper',Speaker='0',ModelFolder='piper_'+name,Styles='|'.join(styles),StyleReason='Giọng nguồn đọc sách và nhịp kể liền mạch gợi ý dùng kể chuyện, tài liệu, hướng dẫn. Đây là gợi ý, cần thử trên lời của bạn.',Sample='samples/curated/'+sample,StarSource=''))
with (base/'voice_profiles.tsv').open('w',encoding='utf-8-sig',newline='')as f:
 w=csv.DictWriter(f,fieldnames=cols,delimiter='\t',lineterminator='\n');w.writeheader();w.writerows(rows)
with(base/'voices.csv').open('w',encoding='utf-8-sig',newline='')as f:
 w=csv.DictWriter(f,fieldnames=cols);w.writeheader();w.writerows(rows)
print('Profiles:',len(rows),'counts',dict(collections.Counter(p['Engine']for p in rows)),'stars',sum(p['Recommended']=='1'for p in rows))
(q/'release_profiles.json').write_text(json.dumps(rows,indent=2,ensure_ascii=False))
