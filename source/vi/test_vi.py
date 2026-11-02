import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile

import vi_worker as worker
import setup_vi as setup


class VietnameseTests(unittest.TestCase):
    def test_unicode(self):
        self.assertEqual(worker.normalize('Tiếng Việt\n  rõ ràng.'), 'Tiếng Việt rõ ràng.')
        self.assertRaises(ValueError, worker.normalize, 'test\x00')

    def test_turing_policy(self):
        torch = Mock()
        torch.cuda.is_available.return_value = True
        torch.cuda.get_device_name.return_value = 'RTX 2070'
        self.assertEqual(worker.device_policy('gpu', torch)[:2], ('cuda', 'float32'))
        self.assertIn('2070', worker.device_policy('auto', torch)[2])
        torch.cuda.is_available.return_value = False
        self.assertEqual(worker.device_policy('auto', torch)[0], 'cpu')
        self.assertRaises(RuntimeError, worker.device_policy, 'gpu', torch)

    def test_cpu_does_not_import_torch(self):
        self.assertEqual(worker.device_policy('cpu')[0], 'cpu')

    def test_archive_traversal(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d)
            with zipfile.ZipFile(p / 'bad.zip', 'w') as z:
                z.writestr('../outside', 'bad')
            self.assertRaises(ValueError, setup.extract, p / 'bad.zip', p / 'target')
            self.assertFalse((p / 'outside').exists())

    def test_hash_mismatch_does_not_publish(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / 'target'
            with patch('urllib.request.urlopen', side_effect=lambda *a, **kw: contextlib.closing(io.BytesIO(b'bad'))):
                self.assertRaises(RuntimeError, setup.download, 'https://example.invalid/x', path, '0'*64)
            self.assertFalse(path.exists())
            self.assertFalse(path.with_suffix('.part').exists())

    def test_activation(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d)
            home = p / 'pack'; home.mkdir()
            (p / 'active.xml').write_text('<old/>')
            self.assertFalse((home / 'ready.json').exists())
            setup.activate(p, home, 'cpu')
            self.assertEqual(json.loads((home / 'ready.json').read_text())['sdk'], '3.8.3')
            self.assertEqual(setup.ET.parse(p / 'active.xml').getroot().get('folder'), 'pack')

    def test_numbers_dates_and_abbreviations(self):
        from sea_g2p import Normalizer
        normalized = Normalizer().normalize('Ngày 24/10/2026, TP.HCM có giá 125.000 đồng.')
        self.assertIn('hai mươi bốn tháng mười', normalized)
        self.assertIn('thành phố hồ chí minh', normalized)
        self.assertIn('một trăm hai mươi lăm nghìn đồng', normalized)

    def test_curated_names_exist_in_pinned_sdk(self):
        import vieneu
        voices = json.loads((Path(vieneu.__file__).parent / 'assets/voices_v3_turbo.json').read_text())['presets']
        for name in ['Hải Đăng', 'Trúc Ly', 'Thiện Minh', 'Mai Anh', 'Adam bựa', 'Thùy Dung', 'Quang Sơn', 'Ngọc Trân', 'Thái Sơn', 'Kim Thanh']:
            self.assertIn(name, voices)


if __name__ == '__main__':
    unittest.main(verbosity=2)
