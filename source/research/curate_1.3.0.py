import os,json,csv,re,time,wave,sys,pathlib
import numpy as np
import sherpa_onnx
base=pathlib.Path('/tmp/v15-build/package');research=base/'source/research';out=pathlib.Path('/tmp/v15-130-research');samples=out/'auditions';samples.mkdir(exist_ok=True)
metrics={x['key']:x for x in json.loads((research/'voice_metrics.json').read_text())}
profiles={r['Key']:r for r in csv.DictReader((base/'voice_profiles.tsv').open(encoding='utf-8-sig'),delimiter='\t')}
reverse={v:k for k,v in json.load((base/'libritts/en_US-libritts_r-medium.onnx.json').open())['speaker_id_map'].items()}
annotations=[]
for n in [1,2,3]:annotations.append(dict(line.split('|',1) for line in (research/f'df{n}_en.csv').read_text().splitlines()))
def traits(sid):
 vals={}
 for d in annotations:
  for tag in d.get(reverse[sid],'').split(','):
   w=.5 if tag.startswith('slightly ') else 1.5 if tag.startswith('very ') else 1
   tag=re.sub(r'^(slightly |very )','',tag);vals[tag]=vals.get(tag,0)+w
 return vals
def rank(sid):
 d=traits(sid);m=metrics['L:'+str(sid)]
 score=sum(d.get(t,0)*w for t,w in [('clear',3),('fluent',3),('friendly',1),('sincere',1),('reassuring',.7),('calm',.5),('intellectual',.5)])
 score-=sum(d.get(t,0)*w for t,w in [('muffled',3),('halting',3),('nasal',1.2),('hoarse',1),('weak',.7)])
 score-=max(0,m['wpm']-235)*.12+max(0,170-m['wpm'])*.1
 if not 70<=m['hz']<=310:score-=20
 return score
candidates=[]
for gender in ['Nam','Nữ']:
 ids=[i for i in range(904) if profiles['L:'+str(i)]['Gender'].startswith(gender)]
 candidates.extend(sorted(ids,key=lambda i:(-rank(i),i))[:50])
(out/'candidate_ids.json').write_text(json.dumps(candidates))
texts=["Have you ever forgotten why you entered a room? Your brain may have switched contexts at the doorway.","At first, the new camera looked ordinary. Then Sarah noticed one detail nobody could explain. Why did the same red light appear every night? Here is what happened next."]
def norm(t):return re.findall(r"[a-z]+",t.lower().replace('contexts','context'))
def wer(ref,hyp):
 a=norm(ref);b=norm(hyp);d=list(range(len(b)+1))
 for i,x in enumerate(a):
  n=[i+1]
  for j,y in enumerate(b):n.append(min(n[-1]+1,d[j+1]+1,d[j]+(x!=y)))
  d=n
 return d[-1]/len(a)
asr=sherpa_onnx.OfflineRecognizer.from_whisper(encoder='/tmp/v15-asr/tiny.en-encoder.int8.onnx',decoder='/tmp/v15-asr/tiny.en-decoder.int8.onnx',tokens='/tmp/v15-asr/tiny.en-tokens.txt',num_threads=2,language='en',task='transcribe')
def make(kind,folder):
 p=base/folder
 if kind=='libri':cfg=sherpa_onnx.OfflineTtsModelConfig(vits=sherpa_onnx.OfflineTtsVitsModelConfig(model=str(p/'en_US-libritts_r-medium.onnx'),tokens=str(p/'tokens.txt'),data_dir=str(p/'espeak-ng-data')),num_threads=2)
 elif kind=='kitten':cfg=sherpa_onnx.OfflineTtsModelConfig(kitten=sherpa_onnx.OfflineTtsKittenModelConfig(model=str(p/'model.onnx'),voices=str(p/'voices.bin'),tokens=str(p/'tokens.txt'),data_dir=str(p/'espeak-ng-data')),num_threads=2)
 else:cfg=sherpa_onnx.OfflineTtsModelConfig(vits=sherpa_onnx.OfflineTtsVitsModelConfig(model=str(p/'model.onnx'),tokens=str(p/'tokens.txt'),data_dir=str(p/'espeak-ng-data')),num_threads=2)
 return sherpa_onnx.OfflineTts(sherpa_onnx.OfflineTtsConfig(model=cfg,max_num_sentences=4))
def test(model,key,sid):
 cache=out/(key.replace(':','_')+'_qa.json')
 if cache.exists():return json.loads(cache.read_text())
 result={'key':key,'tests':[]}
 for i,text in enumerate(texts):
  audio=model.generate(text,sid=sid,speed=1);x=np.asarray(audio.samples);sr=audio.sample_rate
  s=asr.create_stream();s.accept_waveform(sr,x);asr.decode_stream(s)
  p=samples/(key.replace(':','_')+f'_{i}.wav')
  with wave.open(str(p),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes(np.rint(np.clip(x,-1,1)*32767).astype('<i2').tobytes())
  result['tests'].append({'text':text,'recognized':s.result.text,'wer':round(wer(text,s.result.text),4),'seconds':round(len(x)/sr,3),'rms':round(float(np.sqrt(np.mean(x*x))),4),'clip_fraction':float(np.mean(np.abs(x)>=.999))})
 result['wer']=sum(t['wer'] for t in result['tests'])/2;result['traits_rank']=rank(sid) if key.startswith('L:') else None
 cache.write_text(json.dumps(result,ensure_ascii=False,indent=2));return result
m=make('libri','libritts');results=[];start=time.time()
for i,sid in enumerate(candidates):
 r=test(m,'L:'+str(sid),sid);results.append(r)
 if i%10==0:print('Libri QA',i+1,'/100',round(time.time()-start),'s',flush=True)
selected=[]
for gender in ['Nam','Nữ']:
 eligible=[r for r in results if profiles[r['key']]['Gender'].startswith(gender) and r['wer']<=.06 and all(t['clip_fraction']<.005 and .015<t['rms']<.4 for t in r['tests'])]
 eligible.sort(key=lambda r:(r['wer'],-r['traits_rank']))
 selected.extend(r['key'] for r in eligible[:10])
(out/'selected_libri.json').write_text(json.dumps(selected));(out/'libri_selection_qa.json').write_text(json.dumps(results,indent=2,ensure_ascii=False));print('SELECTED',selected,flush=True)
del m
for kind,folder,ids in [('kitten','kitten',range(8)),('piper','piper_john',[0]),('piper','piper_kristin',[0])]:
 m=make(kind,folder)
 for sid in ids:
  key='T:'+str(sid) if kind=='kitten' else ('P:john' if folder.endswith('john') else 'P:kristin')
  r=test(m,key,sid);print('NEW',key,r['wer'],flush=True)
 del m
print('QA FINISHED',flush=True)
