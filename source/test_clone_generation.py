"""Failure injection tests on the real generation policy, without model weights."""
import contextlib,importlib.util,pathlib,sys,types,unittest
from unittest.mock import patch
import numpy as np
PATH=pathlib.Path(__file__).with_name('ai_worker.py')
spec=importlib.util.spec_from_file_location('v15_worker_policy',PATH);worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)
class Torch:
 def __init__(self):self.seeds=[]
 def manual_seed(self,n):self.seeds.append(n)
class Model:
 def __init__(self,answers):
  self.answers=answers;self.calls=[];self.prompt_seen=[]
  self.model=types.SimpleNamespace(talker=types.SimpleNamespace(generate=self.talker))
 def talker(self,**kw):
  for criterion in kw.get('stopping_criteria',[]):criterion(None,None)
 def generate_voice_clone(self,**kw):
  self.calls.append(kw);self.model.talker.generate();answer=self.answers[min(len(self.calls)-1,len(self.answers)-1)]
  if isinstance(answer,Exception):raise answer
  return [answer],24000
 def generate_custom_voice(self,**kw):return self.generate_voice_clone(**kw)
def audio(seconds=1):return np.sin(np.arange(int(24000*seconds))*.04).astype('float32')*.2
class GenerationTests(unittest.TestCase):
 def run_model(self,model,kind='qwen_base',offset=0):
  torch=Torch();messages=[]
  with patch.dict(sys.modules,{'transformers':types.SimpleNamespace(StoppingCriteriaList=list)}):
   result=worker.synthesize_segment(model,kind,'ADAM',['REFERENCE'],'This is the complete target sentence.',torch,np,'cuda:0',offset,messages.append)
  return result,torch,messages
 def test_valid_first_attempt(self):
  m=Model([audio()]);result,t,msg=self.run_model(m);self.assertEqual(len(m.calls),1);self.assertEqual(result[1],24000);self.assertEqual(m.calls[0]['voice_clone_prompt'],['REFERENCE'])
 def test_long_then_valid(self):
  m=Model([audio(30),audio()]);result,t,msg=self.run_model(m);self.assertEqual(len(m.calls),2);self.assertNotEqual(t.seeds[0],t.seeds[1]);self.assertTrue(m.calls[0]['do_sample']);self.assertTrue(m.calls[0]['subtalker_dosample']);self.assertLess(m.calls[1]['temperature'],m.calls[0]['temperature']);self.assertGreater(m.calls[1]['repetition_penalty'],m.calls[0]['repetition_penalty'])
 def test_exhausts_at_three(self):
  m=Model([audio(30)])
  with self.assertRaisesRegex(worker.BadSpeech,'SYNTH_INVALID'):self.run_model(m)
  self.assertEqual(len(m.calls),3)
 def test_empty_retried(self):
  m=Model([np.array([],dtype='float32'),audio()]);self.run_model(m);self.assertEqual(len(m.calls),2)
 def test_nan_retried(self):
  m=Model([np.array([np.nan]*24000),audio()]);self.run_model(m);self.assertEqual(len(m.calls),2)
 def test_infinity_retried(self):
  m=Model([np.array([np.inf]*24000),audio()]);self.run_model(m);self.assertEqual(len(m.calls),2)
 def test_silence_retried(self):
  m=Model([np.zeros(24000),audio()]);self.run_model(m);self.assertEqual(len(m.calls),2)
 def test_short_retried(self):
  m=Model([audio(.05),audio()]);self.run_model(m);self.assertEqual(len(m.calls),2)
 def test_token_ceiling_not_published(self):
  text='Hello.';limit,cap=worker.speech_limits(text)
  with self.assertRaises(worker.BadSpeech):worker.validate_speech(audio(cap/12.5),24000,text,cap,np)
 def test_bad_sample_rate(self):
  for sr in [0,float('nan'),192000]:
   with self.assertRaises(worker.BadSpeech):worker.validate_speech(audio(),sr,'Hello',160,np)
 def test_custom_voice_retry(self):
  m=Model([audio(30),audio()]);self.run_model(m,'qwen_custom');self.assertEqual(m.calls[0]['speaker'],'ADAM');self.assertEqual(len(m.calls),2)
 def test_model_exception_not_hidden(self):
  m=Model([ValueError('reference transcript is invalid')]);original=m.model.talker.generate
  with self.assertRaisesRegex(ValueError,'reference transcript'):self.run_model(m)
  self.assertEqual(len(m.calls),1);self.assertEqual(m.model.talker.generate,original)
 def test_cuda_failure_delegates_to_parent(self):
  m=Model([RuntimeError('CUDA out of memory')])
  with self.assertRaisesRegex(RuntimeError,'CUDA'):self.run_model(m)
  self.assertEqual(len(m.calls),1)
 def test_deadline_reaches_inner_talker_even_if_outer_drops_kwargs(self):
  m=Model([audio()]);original=m.model.talker.generate
  with patch.object(worker.time,'monotonic',side_effect=[0,99999]*3):
   with self.assertRaisesRegex(worker.BadSpeech,'SYNTH_INVALID'):self.run_model(m)
  self.assertEqual(len(m.calls),3);self.assertEqual(m.model.talker.generate,original)
 def test_new_worker_offset_changes_seeds(self):
  _,a,_=self.run_model(Model([audio()]),offset=0);_,b,_=self.run_model(Model([audio()]),offset=1);self.assertNotEqual(a.seeds,b.seeds)
 def test_text_split_preserves_words_and_budget(self):
  text=' '.join(['word']*73);parts=worker.split_text(text);self.assertEqual(' '.join(parts),text);self.assertTrue(all(len(p.split())<=24 for p in parts))
 def test_short_sentence_not_fragmented_into_single_words(self):
  text='Dr. Smith reads this sentence.';self.assertEqual(worker.split_text(text),[text])
 def test_internal_parts_have_finite_bounded_budget(self):
  for count in [1,8,24,1000]:
   seconds,cap=worker.speech_limits(' '.join(['hello']*count));self.assertTrue(96<=cap<=768);self.assertTrue(seconds>=10)
if __name__=='__main__':unittest.main(verbosity=2)
