import unittest
from subtitle_quality import verify_segments,candidate_evidence
from subtitle_core import assemble
from subtitle_phrases import boundary_rules
class Completion149(unittest.TestCase):
 def words(self,text,start=0,step=.25):return [dict(word=w,start=start+i*step,end=start+(i+1)*step,flags=[]) for i,w in enumerate(text.split())]
 def segments(self,text):
  w=self.words(text);return [dict(text=text,start=0,end=w[-1]['end'],words=w)]
 def test_default_does_not_normalize_by_running_more_crops(self):
  text='He was driving along and he sees the the gas station that he always passed.';calls=[]
  def recognize(a,b):
   calls.append((a,b));return self.words(text if len(calls)==1 else text.replace('the the','the'))
  s,log=verify_segments(self.segments(text),20,recognize)
  self.assertEqual(s[0]['text'],text);self.assertFalse(log);self.assertEqual(calls,[])
 def test_nearby_word_disagreement_does_not_hide_duplicate(self):
  a=self.words('He sees the the gas station that he always passed.');b=self.words('He sees the gas station which he always passed.')
  self.assertTrue(candidate_evidence(a,b,3,4))
 def test_unrelated_repeated_sentence_cannot_vote(self):
  a=self.words('He sees the the gas station that he always passed.');b=self.words('He sees the gas station that he always passed.',30)
  self.assertFalse(candidate_evidence(a,b,3,4))
 def test_default_repetition_is_retained_with_review_info(self):
  text='He was driving along and he sees the the gas station that he always passed.';s,log=verify_segments(self.segments(text),20,lambda a,b:self.words(text));self.assertEqual(s[0]['text'],text);self.assertFalse(log);self.assertTrue(any('Giữ nguyên' in f for w in s[0]['words'] for f in w.get('info',[])))
 def test_three_reported_grammar_boundaries_are_protected(self):
  for text,phrase in [('He looked so pale and he smelled like vomit before he left.','smelled like vomit'),('John he eventually manages to turn the car on and he throws it in gear before leaving.','throws it'),('And so John sees her and forgets all about his friend Frankie being gone and just smiles back at this girl.','being gone')]:
   c=assemble(self.words(text),20);self.assertTrue(any(phrase in a['text'].replace('\n',' ') for a in c),[a['text'] for a in c]);self.assertEqual(' '.join(a['text'].replace('\n',' ') for a in c),text)
 def test_noun_phrase_and_auxiliary(self):
  w=self.words('He had completely lost his old car keys.');bad=boundary_rules(w)
  self.assertIn(2,bad);self.assertIn(3,bad);self.assertIn(5,bad);self.assertIn(6,bad)
 def test_oversized_phrase_preserves_all_words(self):
  text='He saw the '+('extraordinary '*30)+'machine yesterday.';c=assemble(self.words(text,step=.15),15,True);self.assertEqual(' '.join(a['text'].replace('\n',' ') for a in c),text)
if __name__=='__main__':unittest.main()
