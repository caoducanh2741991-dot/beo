import pathlib,json,re,wave,time
import sherpa_onnx,numpy as np
p=pathlib.Path('/tmp/v15-build/package');q=pathlib.Path('/tmp/v15-130-research');ids=json.loads((q/'selected_libri.json').read_text());texts=json.loads((q/'L_291_qa.json').read_text())['tests'];a=pathlib.Path('/tmp/v15-asr')
asr=sherpa_onnx.OfflineRecognizer.from_whisper(encoder=str(a/'tiny.en-encoder.int8.onnx'),decoder=str(a/'tiny.en-decoder.int8.onnx'),tokens=str(a/'tiny.en-tokens.txt'),num_threads=2,language='en',task='transcribe')
def norm(t):return re.findall('[a-z]+',t.lower().replace('contexts','context'))
def wer(x,y):
 a,b=norm(x),norm(y);d=list(range(len(b)+1))
 for i,x in enumerate(a):
  n=[i+1]
  for j,y in enumerate(b):n.append(min(n[-1]+1,d[j+1]+1,d[j]+(x!=y)))
  d=n
 return d[-1]/len(a)
results=[]
for folder,keys in [('libritts',ids),('piper_john',['P:john']),('piper_kristin',['P:kristin'])]:
 model=p/folder;cfg=sherpa_onnx.OfflineTtsModelConfig(vits=sherpa_onnx.OfflineTtsVitsModelConfig(model=str(model/('en_US-libritts_r-medium.onnx'if folder=='libritts'else'model.onnx')),tokens=str(model/'tokens.txt'),data_dir=str(model/'espeak-ng-data'),noise_scale_w=0),num_threads=2);tts=sherpa_onnx.OfflineTts(sherpa_onnx.OfflineTtsConfig(model=cfg,max_num_sentences=4))
 for key in keys:
  tests=[]
  for i,t in enumerate(texts):
   audio=tts.generate(t['text'],sid=int(key[2:])if key.startswith('L:')else 0,speed=1);stream=asr.create_stream();stream.accept_waveform(audio.sample_rate,audio.samples);asr.decode_stream(stream);w=wer(t['text'],stream.result.text);tests.append({'text':t['text'],'recognized':stream.result.text,'wer':w,'seconds':len(audio.samples)/audio.sample_rate})
   if i==0:
    with wave.open(str(p/'samples/curated'/(key.replace(':','_')+'.wav')),'wb')as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(audio.sample_rate);f.writeframes(np.rint(np.clip(audio.samples,-1,1)*32767).astype('<i2').tobytes())
  result={'key':key,'tests':tests,'wer':sum(x['wer']for x in tests)/2};results.append(result);print(key,round(result['wer'],4),flush=True)
 del tts
(q/'stable_vits_results.json').write_text(json.dumps(results,indent=2));print('DONE',len(results),flush=True)
