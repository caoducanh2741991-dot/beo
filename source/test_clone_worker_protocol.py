"""Actual worker subprocess + fake Qwen + real numpy/soundfile/FFmpeg."""
import base64,json,os,pathlib,shutil,subprocess,sys,tempfile,unittest
import numpy as np
import soundfile as sf
SOURCE=pathlib.Path(__file__).resolve().parent
TORCH='''import contextlib
float32='float32'
def manual_seed(n):pass
def set_num_threads(n):pass
def set_num_interop_threads(n):pass
def inference_mode():return contextlib.nullcontext()
class Cuda:
 def is_available(self):return False
cuda=Cuda()
'''
QWEN='''from pathlib import Path
import json,types,numpy as np
import onnxruntime
ROOT=Path(__file__).parent
class Talker:
 def generate(self,**kwargs):
  for stop in kwargs.get('stopping_criteria',[]):stop(None,None)
class Qwen3TTSModel:
 @classmethod
 def from_pretrained(cls,*a,**kw):return cls()
 def __init__(self):self.model=types.SimpleNamespace(talker=Talker());self.sr=24000
 def create_voice_clone_prompt(self,**kw):
  (ROOT/'prompt.json').write_text(json.dumps(kw));return ['REFERENCE_CACHE']
 def generate_voice_clone(self,**kw):
  self.model.talker.generate()
  with (ROOT/'calls.jsonl').open('a') as f:f.write(json.dumps({k:v for k,v in kw.items() if k!='stopping_criteria'})+'\\n')
  n=len((ROOT/'calls.jsonl').read_text().splitlines());mode=(ROOT/'scenario').read_text()
  if mode=='badall' or (mode=='badonce' and n==1):seconds=30
  else:seconds=1
  return [np.sin(np.arange(int(seconds*24000))*.04).astype('float32')*.3],24000
 def generate_custom_voice(self,**kw):return self.generate_voice_clone(**kw)
'''
class ProtocolTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory(prefix='v15-clone-protocol-');self.root=pathlib.Path(self.tmp.name)
  for old,new in [('ai_worker.py','worker.py'),('gpu_policy.py','gpu_policy.py')]:shutil.copyfile(SOURCE/old,self.root/new)
  for name,text in [('torch.py',TORCH),('qwen_tts.py',QWEN),('transformers.py','StoppingCriteriaList=list\n'),('onnxruntime.py','')]: (self.root/name).write_text(text)
  sf.write(self.root/'reference.wav',np.sin(np.arange(240000)*.05)*.3,24000)
  (self.root/'scenario').write_text('success');self.process=None;self.stderr=(self.root/'errors.txt').open('w');self.env=dict(os.environ)
 def tearDown(self):
  if self.process:
   self.process.terminate()
   try:self.process.wait(timeout=3)
   except subprocess.TimeoutExpired:self.process.kill();self.process.wait(timeout=3)
   self.process.stdin.close();self.process.stdout.close()
  self.stderr.close();self.tmp.cleanup()
 def start(self,transcript='',reference=None):
  args=[sys.executable,'-u',str(self.root/'worker.py'),'--device','cpu','--model','qwen_base','--reference',str(reference or self.root/'reference.wav'),'--ref-text',transcript]
  self.process=subprocess.Popen(args,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=self.stderr,text=True,env=self.env)
  return self.wait()
 def wait(self):
  self.status=[]
  while True:
   line=self.process.stdout.readline()
   if not line:raise RuntimeError((self.root/'errors.txt').read_text())
   if not line.startswith('V15_'):continue
   kind,value=line.rstrip('\r\n').split('\t');value=base64.b64decode(value).decode()
   if kind=='V15_STATUS':self.status.append(value);continue
   return kind,value
 def synth(self,text='All target words remain in this sentence.',speed=1):
  out=self.root/'out.wav';b=lambda s:base64.b64encode(s.encode()).decode()
  self.process.stdin.write('SYNTH\t'+b(text)+'\t'+b(str(out))+'\t'+str(speed)+'\n');self.process.stdin.flush();return self.wait()
 def calls(self):return [json.loads(s) for s in (self.root/'calls.jsonl').read_text().splitlines()]
 def test_reference_embedding_mode_and_benign_onnx_import(self):
  self.assertEqual(self.start()[0],'V15_READY');prompt=json.loads((self.root/'prompt.json').read_text());self.assertTrue(prompt['x_vector_only_mode']);self.assertEqual(self.synth()[0],'V15_OK');self.assertTrue(self.calls()[0]['do_sample']);self.assertEqual(self.calls()[0]['voice_clone_prompt'],['REFERENCE_CACHE'])
 def test_reference_transcript_mode(self):
  self.assertEqual(self.start('Exact reference words.')[0],'V15_READY');prompt=json.loads((self.root/'prompt.json').read_text());self.assertFalse(prompt['x_vector_only_mode']);self.assertEqual(prompt['ref_text'],'Exact reference words.')
 def test_invalid_first_audio_is_replaced_by_valid_retry(self):
  (self.root/'scenario').write_text('badonce');self.start();self.assertEqual(self.synth()[0],'V15_OK');self.assertEqual(len(self.calls()),2);self.assertTrue(any('lần thử 2/3' in s for s in self.status));info=sf.info(self.root/'out.wav');self.assertEqual((info.samplerate,info.channels,info.subtype),(24000,1,'PCM_16'));self.assertLess(info.duration,2)
 def test_three_invalid_results_never_overwrite_existing_output(self):
  (self.root/'scenario').write_text('badall');self.start();(self.root/'out.wav').write_bytes(b'KEEP EXISTING WAV');kind,error=self.synth();self.assertEqual(kind,'V15_ERR');self.assertIn('SYNTH_INVALID',error);self.assertEqual(len(self.calls()),3);self.assertEqual((self.root/'out.wav').read_bytes(),b'KEEP EXISTING WAV');self.assertFalse(list(self.root.glob('*.v15.tmp.wav')))
 def test_speed_only_request_reuses_accepted_natural_audio(self):
  self.start();self.assertEqual(self.synth()[0],'V15_OK');normal=sf.info(self.root/'out.wav').duration;self.assertEqual(self.synth(speed=1.2)[0],'V15_OK');self.assertEqual(len(self.calls()),1);self.assertLess(sf.info(self.root/'out.wav').duration,normal);self.assertTrue(any('Dùng lại' in s for s in self.status))
 def test_reference_too_short_is_actionable(self):
  sf.write(self.root/'short.wav',np.ones(24000)*.2,24000);kind,error=self.start(reference=self.root/'short.wav');self.assertEqual(kind,'V15_ERR');self.assertIn('3–30',error)
 def test_silent_reference_is_rejected(self):
  sf.write(self.root/'silent.wav',np.zeros(240000),24000);kind,error=self.start(reference=self.root/'silent.wav');self.assertEqual(kind,'V15_ERR');self.assertIn('im lặng',error)
 def test_bad_conversion_never_overwrites_existing_output(self):
  fake=self.root/'bin';fake.mkdir();exe=fake/'ffmpeg';exe.write_text('#!/bin/sh\nfor out do :; done\nprintf broken > "$out"\nexit 0\n');exe.chmod(0o755);self.env['PATH']=str(fake)+os.pathsep+self.env['PATH']
  self.start();(self.root/'out.wav').write_bytes(b'KEEP WAV');self.assertEqual(self.synth()[0],'V15_ERR');self.assertEqual((self.root/'out.wav').read_bytes(),b'KEEP WAV');self.assertFalse(list(self.root.glob('*.v15.tmp.wav')))
 def test_failed_new_text_does_not_poison_accepted_cache(self):
  self.start();self.assertEqual(self.synth()[0],'V15_OK');(self.root/'scenario').write_text('badall');self.assertEqual(self.synth('A different sentence.')[0],'V15_ERR');self.assertEqual(len(self.calls()),4);(self.root/'scenario').write_text('success');self.assertEqual(self.synth()[0],'V15_OK');self.assertEqual(len(self.calls()),4)
if __name__=='__main__':unittest.main(verbosity=2)
