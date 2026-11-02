# Finish selection using the exact stable-duration settings shipped in the app.
import pathlib,json,csv,re,wave
import sherpa_onnx,numpy as np
p=pathlib.Path('/tmp/v15-build/package');q=pathlib.Path('/tmp/v15-130-research');initial=json.loads((q/'libri_selection_qa.json').read_text());stable={r['key']:r for r in json.loads((q/'stable_vits_results.json').read_text())};profiles={r['Key']:r for r in csv.DictReader((p/'source/research/voice_profiles_all_1.2.0.tsv').open(encoding='utf-8-sig'),delimiter='\t')};texts=initial[0]['tests'];a=pathlib.Path('/tmp/v15-asr')
# Keep a copy of final-setting auditions already generated for the first shortlist.
for key in stable:
 src=p/'samples/curated'/(key.replace(':','_')+'.wav')
 if src.exists():(q/'auditions'/(key.replace(':','_')+'_stable.wav')).write_bytes(src.read_bytes())
asr=sherpa_onnx.OfflineRecognizer.from_whisper(encoder=str(a/'tiny.en-encoder.int8.onnx'),decoder=str(a/'tiny.en-decoder.int8.onnx'),tokens=str(a/'tiny.en-tokens.txt'),num_threads=2,language='en',task='transcribe')
v=p/'libritts';tts=sherpa_onnx.OfflineTts(sherpa_onnx.OfflineTtsConfig(model=sherpa_onnx.OfflineTtsModelConfig(vits=sherpa_onnx.OfflineTtsVitsModelConfig(model=str(v/'en_US-libritts_r-medium.onnx'),tokens=str(v/'tokens.txt'),data_dir=str(v/'espeak-ng-data'),noise_scale_w=0),num_threads=2),max_num_sentences=4))
def norm(s):return re.findall('[a-z]+',s.lower().replace('contexts','context'))
def wer(ref,hyp):
 a,b=norm(ref),norm(hyp);d=list(range(len(b)+1))
 for i,x in enumerate(a):
  n=[i+1]
  for j,y in enumerate(b):n.append(min(n[-1]+1,d[j+1]+1,d[j]+(x!=y)))
  d=n
 return d[-1]/len(a)
def test(key):
 tests=[]
 for i,t in enumerate(texts):
  au=tts.generate(t['text'],sid=int(key[2:]),speed=1);s=asr.create_stream();s.accept_waveform(au.sample_rate,au.samples);asr.decode_stream(s);tests.append({'text':t['text'],'recognized':s.result.text,'wer':wer(t['text'],s.result.text),'seconds':len(au.samples)/au.sample_rate})
  if i==0:
   with wave.open(str(q/'auditions'/(key.replace(':','_')+'_stable.wav')),'wb')as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(au.sample_rate);f.writeframes(np.rint(np.clip(au.samples,-1,1)*32767).astype('<i2').tobytes())
 r={'key':key,'tests':tests,'wer':sum(t['wer']for t in tests)/2};stable[key]=r;print('Additional',key,round(r['wer'],3),flush=True);return r
selected=[]
for gender in ['Nam','Nữ']:
 candidates=[r for r in initial if profiles[r['key']]['Gender'].startswith(gender)and r['wer']<=.06 and all(t['clip_fraction']<.005 and .015<t['rms']<.4 for t in r['tests'])];candidates.sort(key=lambda r:(r['wer'],-r['traits_rank']))
 chosen=[]
 for r in candidates:
  st=stable.get(r['key'])or test(r['key'])
  if st['wer']<=.06:chosen.append(r['key'])
  if len(chosen)==10:break
 if len(chosen)!=10:raise RuntimeError('Fewer than 10 passing '+gender+' candidates; do not fabricate selection')
 selected+=chosen
(q/'selected_libri.json').write_text(json.dumps(selected));(q/'stable_vits_results.json').write_text(json.dumps(list(stable.values()),indent=2));print('FINAL',selected,flush=True)
