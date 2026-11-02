"""Integration of measured-word results with V15.6 hard anchors; no model claims."""
import tempfile
import unittest
from pathlib import Path
from voice_alignment import read_units, make_output


class VoiceAlignmentTests(unittest.TestCase):
    def setUp(self):
        self.segments = [dict(start=0, end=3, text='Keep these words.', stage=1),
                         dict(start=3.2, end=6, text='Another full sentence.', stage=2)]
        self.stages = {1: (0., 3.), 2: (3.2, 6.)}
        self.words = [dict(word=t, start=a, end=b, segment_id=i//3, segment_start=0, segment_end=6, flags=[])
                      for i, (t, a, b) in enumerate([('Keep', .1, .6), ('these', .7, 1.2), ('words.', 1.3, 2.9),
                                                     ('Another', 3.25, 3.8), ('full', 4, 4.5), ('sentence.', 4.7, 5.9)])]

    def test_measured_times_not_search_windows(self):
        cues, words = make_output(self.words, self.segments, self.stages, 6)
        self.assertEqual(words[0]['start'], .1)
        self.assertEqual(cues[0]['start'], .1)
        self.assertTrue(all(c['end'] <= 3 or c['start'] >= 3.2 for c in cues))
        self.assertEqual(' '.join(c['text'].replace('\n', ' ') for c in cues), 'Keep these words. Another full sentence.')

    def test_near_edge_marked_and_clipped(self):
        self.words[2]['end'] = 3.03
        _, words = make_output(self.words, self.segments, self.stages, 6)
        self.assertEqual(words[2]['end'], 3.)
        self.assertIn('Từ sát neo chặng; cần nghe lại', words[2]['flags'])

    def test_far_outside_anchor_rejected(self):
        self.words[2]['end'] = 3.8
        with self.assertRaises(ValueError):
            make_output(self.words, self.segments, self.stages, 6)

    def test_unmeasured_words_rejected(self):
        self.words[0]['start'] = None
        with self.assertRaises(ValueError):
            make_output(self.words, self.segments, self.stages, 6)

    def test_missing_words_rejected(self):
        with self.assertRaises(ValueError):
            make_output(self.words[1:], self.segments, self.stages, 6)

    def test_changed_words_rejected(self):
        self.words[0]['word'] = 'Wrong'
        with self.assertRaises(ValueError):
            make_output(self.words, self.segments, self.stages, 6)

    def test_draft_validation(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d)/'units.xml'
            path.write_text('<units><unit stage="1" start="0" end="3" stageStart="0" stageEnd="3">Known text.</unit></units>')
            segments, stages = read_units(path, 3)
            self.assertEqual(segments[0]['text'], 'Known text.')
            self.assertEqual(stages, {1: (0., 3.)})
            for value in ['nan', '-1', '4', 'inf']:
                path.write_text(f'<units><unit stage="1" start="{value}" end="3" stageStart="0" stageEnd="3">Known text.</unit></units>')
                with self.assertRaises(ValueError):
                    read_units(path, 3)

    def test_draft_overlong_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d)/'units.xml'
            path.write_text('<units><unit stage="1" start="0" end="40" stageStart="0" stageEnd="40">Known text.</unit></units>')
            with self.assertRaises(ValueError):
                read_units(path, 40)


if __name__ == '__main__':
    unittest.main()
