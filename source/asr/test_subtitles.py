import math
import unittest
from subtitle_core import assemble,stamp,wrap
from asr_worker import use_reference,merge_alignment_windows


class Tests(unittest.TestCase):
    def words(self,text,start=2):return [dict(word=w,start=start+i*.4,end=start+i*.4+.3,flags=[]) for i,w in enumerate(text.split())]
    def text(self,cues):return ' '.join(c['text'].replace('\n',' ') for c in cues)
    def test_original_timeline(self):
        c=assemble(self.words('First sentence.')+self.words('Second sentence.',10),15)
        self.assertEqual([(x['start'],x['end']) for x in c],[(2,2.6999999999999997),(10,10.700000000000001)])
    def test_all_words_survive(self):
        text='Every word must survive splitting into short readable subtitles for this original recording.'
        self.assertEqual(self.text(assemble(self.words(text),15,True)),text)
    def test_abbreviation(self):self.assertEqual(len(assemble(self.words('Dr. Smith works here.'),8)),1)
    def test_bad_times(self):
        for a,b in [(math.nan,1),(3,2),(math.inf,4),(2,2.0001),(7,8)]:
            with self.subTest(start=a,end=b),self.assertRaises(ValueError):assemble([dict(word='bad',start=a,end=b)],6)
    def test_reviewed_overlap(self):
        c=assemble([dict(word='one',start=1,end=2),dict(word='two',start=1.9,end=2.5)],3)
        self.assertTrue(c[0]['flags'])
    def test_real_asr_fallback(self):
        c=assemble([dict(word='closer.',start=57.945,end=58.35),dict(word='The',start=58.03,end=58.313,asr_start=58.18,asr_end=58.68),dict(word='next',start=58.859,end=59.102)],62)
        self.assertEqual(self.text(c),'closer. The next');self.assertTrue(any('ASR' in f for x in c for f in x['flags']))
    def test_reference(self):
        s=[dict(start=1,end=5,text='The clock maker repaired the old watch.'),dict(start=5.5,end=9,text='His daughter came back home.')]
        text='The clockmaker repaired the old watch. His daughter came back home.'
        b=use_reference(s,text);self.assertEqual(' '.join(x['text'] for x in b),text);self.assertEqual((b[0]['start'],b[-1]['end']),(1,9))
    def test_wrong_reference(self):
        with self.assertRaises(ValueError):use_reference([dict(start=0,end=3,text='A man fixed an old car.')],'Entirely different words about weather and the sky.')
    def test_merge_short_fragments(self):
        b=merge_alignment_windows([dict(start=0,end=.2,text='He'),dict(start=.8,end=3,text='always changed the subject.')]);self.assertEqual(len(b),1)
    def test_word_wrap(self):
        text='A pneumonoultramicroscopicsilicovolcanoconiosis word stays intact.'
        self.assertEqual(wrap(text).replace('\n',' '),text)
    def test_stamp(self):self.assertEqual(stamp(59.9996),'00:01:00,000')


if __name__=='__main__':unittest.main()
