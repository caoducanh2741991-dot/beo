import unittest,tempfile,json,copy
from pathlib import Path
import numpy as np
from subtitle_quality import verify_segments
from subtitle_core import assemble,write_result
from subtitle_timing import refine_word_ends,review_cue_ends
from asr_worker import read_cache,attach_audio_anchors

class Speech1410Tests(unittest.TestCase):
 def words(self,text,step=.25):return [dict(word=w,start=i*step,end=(i+1)*step,flags=[]) for i,w in enumerate(text.split())]
 def segments(self,text):
  w=self.words(text);return [dict(text=text,start=0,end=w[-1]['end'],words=w)]
 def test_default_never_normalizes_spoken_repetition(self):
  for text in ['He sees the the gas station along the way.',"His eyes snap back because he's he realizes he is swerving.",'He went home went home to his parents.']:
   calls=[]
   s,a=verify_segments(self.segments(text),20,lambda *x:calls.append(x))
   self.assertEqual(' '.join(x['text'] for x in s),text);self.assertEqual(a,[]);self.assertEqual(calls,[]);self.assertTrue(any(w['flags'] for w in s[0]['words']))
 def test_false_start_kept_even_in_explicit_clean_mode(self):
  text="He suddenly woke up because he's he realizes he is late."
  s,a=verify_segments(self.segments(text),20,lambda *x:self.words(text.replace("he's he",'he')),repetition_policy='clean')
  self.assertEqual(s[0]['text'],text);self.assertEqual(a,[])
 def test_conflicting_audio_window_vetoes_cleaning(self):
  text='He was driving along and he sees the the gas station that he always passed.';calls=[]
  def recognize(*x):
   calls.append(x);return self.words(text if len(calls)==1 else text.replace('the the','the'))
  s,a=verify_segments(self.segments(text),20,recognize,repetition_policy='clean')
  self.assertEqual(s[0]['text'],text);self.assertFalse(a)
 def test_explicit_clean_mode_audits_consensus(self):
  text='He was driving along and he sees the the gas station that he always passed.'
  s,a=verify_segments(self.segments(text),20,lambda *x:self.words(text.replace('the the','the')),repetition_policy='clean')
  self.assertEqual(s[0]['text'],text.replace('the the','the'));self.assertEqual(a[0]['policy'],'clean');self.assertGreaterEqual(a[0]['support'],2)
 def test_grammar_and_emphasis_not_flagged_as_errors(self):
  s,a=verify_segments(self.segments('He had had a really really bad day.'),20,lambda *x:[])
  self.assertFalse(a);self.assertFalse(any(w['flags'] for w in s[0]['words']))
 def test_reported_phrase_pairs_within_cues(self):
  cases=[('He takes his beer and he begins walking towards this girl.','he begins walking'),('And John goes to his truck and climbs into the seat.','John goes'),('He starts the car and Frankie had gone around the other side.','Frankie had gone'),('Frankie looks over at John and sees him unable to start.','Frankie looks'),('And then he actually began driving down the main road.','he actually began driving'),('John was white knuckling it the rest of the way.','the rest of the way'),('He had no idea how he got there and looked down.','how he got there'),('And then there were also these two figures standing over him.','then there were')]
  for text,phrase in cases:
   cues=assemble(self.words(text),20);self.assertEqual(' '.join(c['text'].replace('\n',' ') for c in cues),text);self.assertTrue(any(phrase in c['text'].replace('\n',' ') for c in cues),phrase)
 def audio(self,finish=.5):
  t=np.arange(16000)/16000;return np.where(t<finish,.2*np.sin(2*np.pi*220*t),0).astype('float32')
 def test_silent_tail_requires_independent_anchor(self):
  w=[dict(word='word',start=.1,end=.95,asr_end=.5,flags=[])];r=refine_word_ends(w,self.audio());self.assertLess(r[0]['end'],.6);self.assertEqual(w[0]['end'],.95);self.assertIn('ctc_end_before',r[0])
 def test_never_trim_audible_tail_or_unanchored_words(self):
  w=[dict(word='word',start=.1,end=.95,asr_end=.5,flags=[])]
  self.assertEqual(refine_word_ends(w,self.audio(.95))[0]['end'],.95)
  del w[0]['asr_end'];self.assertEqual(refine_word_ends(w,self.audio())[0]['end'],.95)
 def test_quiet_interval_must_agree_with_anchor(self):
  w=[dict(word='word',start=.1,end=.95,asr_end=.25,flags=[])]
  self.assertEqual(refine_word_ends(w,self.audio(.65))[0]['end'],.95)
 def test_low_level_speech_and_music_not_assumed_silent(self):
  w=[dict(word='word',start=.1,end=.95,asr_end=.5,flags=[])]
  t=np.arange(16000)/16000;audio=self.audio()+(.03*np.sin(2*np.pi*300*t)).astype('float32')
  self.assertEqual(refine_word_ends(w,audio)[0]['end'],.95)
 def test_end_disagreement_flag_does_not_move_cue(self):
  cues=[dict(text='A word',start=0,end=1,flags=[])];w=[dict(word='A',end=.2),dict(word='word',end=1,asr_end=.5)]
  r=review_cue_ends(cues,w);self.assertEqual(r[0]['end'],1);self.assertTrue(r[0]['flags'])
 def test_cache_policy_is_part_of_validation(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/'cache.json';p.write_text(json.dumps(dict(schema='v1410-speech-1',repetitions='clean',segments=self.segments('A short sentence.'))))
   self.assertIsNone(read_cache(p,'preserve'));self.assertIsNotNone(read_cache(p,'clean'))
 def test_refine_anchors_never_replace_user_words(self):
  text='He said an unusual name before leaving.';s=self.segments(text)
  for w in s[0]['words']:w['timing_source']='ctc'
  raw=self.segments('He said a different name before leaving.')
  out=attach_audio_anchors(s,raw)
  self.assertEqual(out[0]['text'],text);self.assertEqual([w['word'] for w in out[0]['words']],text.split())
  self.assertTrue(any('asr_end' in w for w in out[0]['words']))
 def test_ctc_measurements_are_not_independent_asr_anchors(self):
  s=self.segments('He said the same words before leaving.');raw=copy.deepcopy(s)
  for w in raw[0]['words']:w['timing_source']='ctc'
  out=attach_audio_anchors(s,raw);self.assertFalse(any('asr_end' in w for w in out[0]['words']))
 def test_remote_occurrence_cannot_supply_end_anchor(self):
  s=self.segments('He said the same words before leaving.');raw=copy.deepcopy(s)
  for w in raw[0]['words']:w['start']+=30;w['end']+=30
  out=attach_audio_anchors(s,raw);self.assertFalse(any('asr_end' in w for w in out[0]['words']))
 def test_xml_saves_repetition_policy_and_measured_tail_evidence(self):
  import xml.etree.ElementTree as E
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/'result.xml';w=[dict(word='word',start=.1,end=.5,asr_end=.48,ctc_end_before=.95,flags=[])];c=[dict(text='word',start=.1,end=.5,flags=[])]
   write_result(p,c,w,1,'cpu','test');r=E.parse(p).getroot();self.assertEqual(r.get('repetitions'),'preserve');self.assertEqual(r.get('pipeline'),'v1410-speech-1');self.assertEqual(r.find('words/word').get('ctc_end_before'),'0.95')
if __name__=='__main__':unittest.main()
