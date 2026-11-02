import copy,unittest,sys,re
from subtitle_quality import *
from subtitle_core import assemble
from asr_worker import retry_weak_alignment,read_cache
from pathlib import Path
import tempfile,json

class QualityTests(unittest.TestCase):
 def words(self,text,start=0,step=.25):return [dict(word=w,start=start+i*step,end=start+(i+1)*step,flags=[]) for i,w in enumerate(text.split())]
 def segments(self,text):
  words=self.words(text);return [dict(start=0,end=words[-1]['end'],text=text,words=words)]
 def test_native_word_pieces(self):
  w=map_words('A 21-year-old man.',self.words('A 21 -year -old man.'));self.assertEqual([x['word'] for x in w],['A','21-year-old','man.']);self.assertEqual((w[1]['start'],w[1]['end']),(.25,1))
 def test_mismatched_tokens_are_not_assigned_foreign_times(self):
  self.assertTrue(all(w['start'] is None for w in map_words('A different sentence.',self.words('A hidden detail.'))))
 def test_protected_grammar_and_emphasis(self):
  w=self.words('He had had a really really bad day and that that mattered.');self.assertEqual(candidates(w),[])
 def test_duplicate_requires_two_audio_windows(self):
  original='He was also on a winning streak playing playing pool at the bar.'
  alternate=self.words('He was also on a winning streak playing pool at the bar.')
  calls=[]
  def recognize(a,b):calls.append((a,b));return alternate
  s,audit=verify_segments(self.segments(original),10,recognize,repetition_policy="clean")
  self.assertEqual(' '.join(x['text'] for x in s),'He was also on a winning streak playing pool at the bar.');self.assertEqual(len(audit),1);self.assertEqual(len(calls),2);self.assertNotEqual(calls[0],calls[1])
 def test_disagreeing_second_window_keeps_repeat(self):
  text='He was also on a winning streak playing playing pool at the bar.';calls=[]
  def recognize(a,b):calls.append(1);return self.words(text.replace('playing playing','playing') if len(calls)==1 else text)
  s,a=verify_segments(self.segments(text),10,recognize,repetition_policy="clean");self.assertEqual(s[0]['text'],text);self.assertFalse(a);self.assertTrue(any(w['flags'] for w in s[0]['words']))
 def test_spoken_repeat_kept(self):
  text='The story was just just awful for all of them.'
  s,a=verify_segments(self.segments(text),10,lambda a,b:self.words(text));self.assertEqual(s[0]['text'],text);self.assertFalse(a)
 def test_duplicate_evidence_can_attach_to_first_copy(self):
  a=self.words('see Frankie who looked just just awful he looked so pale and sick');b=self.words('see Frankie who looked just awful he looked so pale and sick');self.assertIn(5,deletion_votes(a,b))
 def test_reported_false_start_variants(self):
  for old,new in [('the the gas station','the gas station')]:
   source='And now he said '+old+' on that particular night.';alternate=self.words('And now he said '+new+' on that particular night.')
   s,a=verify_segments(self.segments(source),20,lambda a,b:alternate,repetition_policy="clean")
   self.assertEqual(s[0]['text'],'And now he said '+new+' on that particular night.');self.assertEqual(len(a),1)
 def test_missing_repeat_timestamp_uses_measured_neighbour(self):
  s=self.segments('He was also on a winning streak playing playing pool at the bar.');s[0]['words'][9]['start']=None;s[0]['words'][9]['end']=None
  alt=self.words('He was also on a winning streak playing pool at the bar.')
  result,a=verify_segments(s,20,lambda a,b:alt,repetition_policy="clean");self.assertEqual(len(a),1)
 def test_window_edges_cannot_delete(self):self.assertFalse(deletion_votes(self.words('playing playing pool'),self.words('playing pool')))
 def test_verifier_cannot_rewrite_meaning(self):
  text='I am just fine and I can drive safely.';w=self.words(text);transfer_punctuation(w,self.words('I am not fine and I can drive safely.'));self.assertEqual([norm(x['word']) for x in w],[norm(x['word']) for x in self.words(text)])
 def test_only_punctuation_case_transfer(self):
  w=self.words('he goes home then john walks upstairs and falls asleep')
  transfer_punctuation(w,self.words('He goes home. Then John walks upstairs and falls asleep.'))
  self.assertEqual([norm(x['word']) for x in w],[norm(x['word']) for x in self.words('he goes home then john walks upstairs and falls asleep')]);self.assertEqual(w[2]['word'],'home.');self.assertEqual(w[4]['word'],'John')
 def test_unconfirmed_long_run_flagged(self):
  text=' '.join('word'+str(i) for i in range(50));s,a=verify_segments(self.segments(text),20,lambda a,b:[]);self.assertTrue(all(w['flags'] for w in s[0]['words']))
 def test_phrase_repeat(self):self.assertIn((5,7),candidates(self.words('And so he went home went home to his parents.')))
 def test_context_punctuation_fallback_preserves_every_word(self):
  text=' '.join('word'+str(i) for i in range(45));calls=[]
  def punctuate():
   calls.append(1);w=self.words(text);w[14]['word']+='.';w[29]['word']+='.';return w
  s,a=verify_segments(self.segments(text),20,lambda a,b:[],punctuate=punctuate)
  self.assertEqual(len(calls),1);self.assertEqual([norm(w['word']) for w in s[0]['words']],[norm(w) for w in text.split()]);self.assertFalse(any('Đoạn dài' in f for w in s[0]['words'] for f in w['flags']))
 def test_tiny_sentence_tail_is_merged(self):
  w=self.words('He felt somebody grab his arm and shake him.',step=.24);c=assemble(w,5);self.assertFalse(any(x['end']-x['start']<.8 for x in c));self.assertEqual(' '.join(x['text'].replace('\n',' ') for x in c),'He felt somebody grab his arm and shake him.')
 def test_long_lines_split_without_losing_words(self):
  text='and then as soon as he actually began driving down the main road on the way home the first thing he did was to call his friend.'
  c=assemble(self.words(text),20);self.assertTrue(all(len(l)<=42 for x in c for l in x['text'].splitlines()));self.assertEqual(' '.join(x['text'].replace('\n',' ') for x in c),text)
 def test_large_silence_kept(self):
  c=assemble(self.words('First sentence.')+self.words('Second sentence.',10),20);self.assertEqual(len(c),2);self.assertEqual(c[1]['start'],10)
 def test_invalid_or_old_cache_is_ignored(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/'cache.json'
   for value in ['broken',json.dumps(self.segments('An old cached sentence.')),json.dumps(dict(schema='v1410-speech-1',segments=[dict(text='bad')]))]:
    p.write_text(value);self.assertIsNone(read_cache(p))
   valid=self.segments('A verified sentence.');p.write_text(json.dumps(dict(schema='v1410-speech-1',segments=valid)));self.assertEqual(read_cache(p)[0]["text"],valid[0]["text"]);self.assertEqual([(w["start"],w["end"]) for w in read_cache(p)[0]["words"]],[(w["start"],w["end"]) for w in valid[0]["words"]]);self.assertTrue(all(w["timing_source"]=="asr" for w in read_cache(p)[0]["words"]))
 def test_local_alignment_retry(self):
  raw=self.words('A little word here.');bad=[dict(w,score=.8) for w in raw];bad[1]=dict(word='little',score=.05,start=.3,end=.3)
  def align(a,b,text):return [dict(w,score=.8,start=w['start']-a,end=w['end']-a) for w in raw if w['word'] in text.split()]
  fixed,done=retry_weak_alignment(bad,[w['word'] for w in raw],raw,0,[0]*32000,align);self.assertIn(1,done);self.assertGreater(fixed[1]['end'],fixed[1]['start'])

if __name__=='__main__':unittest.main()
