"""Regression cases for real ASR/CTC zero-duration, contained and missing word times."""
import copy
import math
import random
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from subtitle_core import assemble,write_result


class TimelineRepair(unittest.TestCase):
    def check(self,words,duration=10,compact=False):
        expected=' '.join(str(w['word']) for w in words)
        words=copy.deepcopy(words);cues=assemble(words,duration,compact)
        actual=' '.join(c['text'].replace('\n',' ') for c in cues)
        self.assertEqual(actual,expected)
        previous=0
        for c in cues:
            self.assertGreater(round(c['end']*1000),round(c['start']*1000))
            self.assertGreaterEqual(round(c['start']*1000),round(previous*1000))
            self.assertLessEqual(c['end'],duration);self.assertGreaterEqual(c['start'],0);previous=c['end']
        return cues,words

    def w(self,text,start,end,**kwargs):return dict(word=text,start=start,end=end,flags=[],**kwargs)

    def test_zero_middle(self):
        c,w=self.check([self.w('He',1,1.2),self.w('had',1.3,1.3),self.w('left.',1.4,1.8)])
        self.assertTrue(any(cue['flags'] for cue in c));self.assertIsNone(w[1]['start'])

    def test_zero_prefix(self):
        c,w=self.check([self.w('The',0,0,segment_start=0,segment_end=2),self.w('night',.2,.6)])
        self.assertEqual(c[0]['start'],0)

    def test_zero_suffix(self):
        c,w=self.check([self.w('That',2,2.5),self.w('night.',2.5,2.5,segment_start=2,segment_end=3)])
        self.assertEqual(c[-1]['end'],3)

    def test_contained_word(self):self.check([self.w('last',1,2),self.w('night',1.5,1.8),self.w('changed.',2,3)])

    def test_reversed_word(self):self.check([self.w('Last',1,1.5),self.w('night',2,1.8),self.w('changed.',2,3)])

    def test_outside_media_suffix(self):self.check([self.w('The',8,8.5),self.w('end.',10.4,11,segment_start=8,segment_end=10)])

    def test_none_nan(self):self.check([self.w('This',None,None),self.w('was',math.nan,math.nan),self.w('real.',1,2)])

    def test_all_unresolved_segment(self):
        c,w=self.check([self.w('No',None,None,segment_start=2,segment_end=5),self.w('word',3,3,segment_start=2,segment_end=5),self.w('lost.',None,None,segment_start=2,segment_end=5)])
        self.assertEqual((c[0]['start'],c[-1]['end']),(2,5));self.assertTrue(c[0]['flags'])

    def test_no_timing_at_all_rejected(self):
        with self.assertRaises(ValueError):assemble([self.w('missing',None,None)],10)

    def test_asr_preferred(self):
        c,w=self.check([self.w('closer.',57.945,58.35),self.w('The',58.03,58.313,asr_start=58.18,asr_end=58.68),self.w('next',58.859,59.102)],62)
        self.assertTrue(any('ASR' in f for x in c for f in x['flags']))

    def test_metadata_marks_unresolved(self):
        cues,words=self.check([self.w('A',1,1.5),self.w('word.',None,None)])
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'result.xml';write_result(path,cues,words,10,'test','test')
            w=ET.parse(path).findall('words/word')[1]
            self.assertEqual(w.get('timing'),'unresolved');self.assertIsNone(w.get('start'))

    def test_segment_boundary_keeps_text(self):
        self.check([self.w('First.',1,2,segment_id=0),self.w('Unmapped',2,2,segment_id=1,segment_start=2,segment_end=4),self.w('word.',3,4,segment_id=1),self.w('Next.',5,6,segment_id=2)])

    def test_millisecond_rounding(self):self.check([self.w('A',1,1.2),self.w('tiny',1.2,1.20001),self.w('word.',1.3,1.8)])

    def test_seeded_adversarial_cases(self):
        rng=random.Random(1472)
        for case in range(1000):
            words=[]
            for i in range(rng.randint(2,70)):
                start=.05+i*.12;end=start+.10;kind=rng.randrange(8)
                if kind==0:end=start
                elif kind==1:start,end=None,None
                elif kind==2:start,end=start-.2,end-.25
                elif kind==3:start,end=15+i,16+i
                words.append(self.w('w'+str(i),start,end,segment_id=i//10,segment_start=(i//10)*1.2,segment_end=min(10,(i//10+1)*1.2)))
            with self.subTest(case=case):self.check(words,compact=bool(case%2))


if __name__=='__main__':unittest.main()
