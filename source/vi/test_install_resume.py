"""Exercise the production installer across fresh/resumed/interrupted stages.

Real ZIP extraction, hashes, directories, activation and staging are used. Only
external pip/model/voice subprocesses are replaced; no Windows/CUDA claim.
"""
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import setup_vi as setup


class ResumeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='V15 cài lại Tiếng Việt ')
        self.root = Path(self.temp.name)
        self.folder = self.root / 'packs'; self.folder.mkdir()
        self.source = self.root / 'adapter'; self.source.mkdir()
        self.cache = self.folder / 'downloads'; self.cache.mkdir()
        self.calls = []
        self.saved = self.folder / 'active.xml'; self.saved.write_bytes(b'<previous-active-pack/>')
        spec = {}
        artifacts = [
            ('python-3.12.10-embed-amd64.zip', 'python_url', 'python_sha256', {'python.exe': b'python-fixture', 'python312.zip': b'stdlib-fixture'}),
            ('pip.whl', 'pip_url', 'pip_sha256', {'pip/__init__.py': b'pip-fixture'}),
            ('vieneu-test.whl', 'url', 'sha256', {'vieneu/__init__.py': b'sdk-fixture'}),
        ]
        for name, url_key, hash_key, entries in artifacts:
            archive = self.cache / name
            with zipfile.ZipFile(archive, 'w') as z:
                for entry, value in entries.items(): z.writestr(entry, value)
            spec[url_key] = 'https://example.invalid/' + name
            spec[hash_key] = hashlib.sha256(archive.read_bytes()).hexdigest()
        spec['filename'] = 'vieneu-test.whl'
        (self.source / 'sdk_lock.json').write_text(json.dumps(spec))
        self.here = patch.object(setup, 'HERE', self.source); self.here.start()
        self.space = patch('shutil.disk_usage', return_value=type('Space', (), {'free': 100 * 1024**3})()); self.space.start()
        self.network = patch('urllib.request.urlopen', side_effect=AssertionError('Completed cache downloaded again')); self.network.start()

    def tearDown(self):
        self.network.stop(); self.space.stop(); self.here.stop(); self.temp.cleanup()

    def stage(self):
        home = self.folder / 'vieneu-3.8.3-gpu-existing'
        packages = home / 'runtime' / 'Lib' / 'site-packages'
        packages.mkdir(parents=True)
        (home / '.incomplete').touch()
        (packages / 'keep-torch.dll').write_bytes(b'EXISTING CUDA LIBRARY')
        model = home / 'hf' / 'hub' / 'cached-model'; model.parent.mkdir(parents=True); model.write_bytes(b'EXISTING MODEL')
        return home

    def run_install(self, fail=None, probes=None):
        def runner(command, env, timeout=10800):
            command = [str(x) for x in command]
            self.calls.append(command)
            # Activation must never happen before the final selftests/freeze.
            self.assertEqual(self.saved.read_bytes(), b'<previous-active-pack/>')
            if fail and fail(command):
                raise subprocess.CalledProcessError(2, command)
        with patch.object(setup, 'run', side_effect=runner), patch.object(setup, 'probe_dependencies', side_effect=probes or [[]]), contextlib.redirect_stdout(io.StringIO()) as output:
            setup.install_pack(self.folder, 'gpu', self.root / 'ffmpeg.exe')
        return output.getvalue()

    def test_fresh_install_reaches_all_selftests_then_activates(self):
        output = self.run_install()
        home = self.folder / setup.ET.parse(self.saved).getroot().get('folder')
        self.assertTrue((home / 'ready.json').is_file())
        self.assertFalse((home / '.incomplete').exists())
        self.assertIn('V15_SETUP_OK', output)
        self.assertTrue(any('--warmup' in c for c in self.calls))
        tests = [c for c in self.calls if '--selftest' in c]
        self.assertEqual(len(tests), 2)
        self.assertTrue(any('gpu' in c for c in tests) and any('cpu' in c for c in tests))

    def test_existing_site_packages_preserves_library_and_model(self):
        home = self.stage()
        self.run_install()
        self.assertEqual(setup.ET.parse(self.saved).getroot().get('folder'), home.name)
        self.assertEqual((home / 'runtime/Lib/site-packages/keep-torch.dll').read_bytes(), b'EXISTING CUDA LIBRARY')
        self.assertEqual((home / 'hf/hub/cached-model').read_bytes(), b'EXISTING MODEL')

    def test_interrupted_model_download_then_retry_same_directory(self):
        home = self.stage()
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_install(fail=lambda c: '--fetch-models' in c)
        self.assertTrue((home / '.incomplete').exists())
        self.assertFalse((home / 'ready.json').exists())
        self.assertEqual(self.saved.read_bytes(), b'<previous-active-pack/>')
        self.calls.clear(); self.run_install()
        self.assertEqual(setup.ET.parse(self.saved).getroot().get('folder'), home.name)
        self.assertTrue((home / 'runtime/Lib/site-packages/keep-torch.dll').exists())

    def test_selftest_failure_does_not_activate_then_retry_succeeds(self):
        home = self.stage()
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_install(fail=lambda c: '--selftest' in c)
        self.assertFalse((home / 'ready.json').exists())
        self.assertEqual(self.saved.read_bytes(), b'<previous-active-pack/>')
        self.calls.clear(); self.run_install()
        self.assertTrue((home / 'ready.json').exists())

    def test_only_failed_library_force_repaired(self):
        self.stage()
        self.run_install(probes=[[{'spec': 'soundfile==0.14.0', 'error': 'broken import'}], []])
        repairs = [c for c in self.calls if '--force-reinstall' in c]
        self.assertEqual(len(repairs), 1)
        self.assertIn('soundfile==0.14.0', repairs[0])
        self.assertFalse(any('torch==2.8.0' in c for c in repairs))

    def test_still_broken_dependency_does_not_fetch_or_activate(self):
        home = self.stage()
        failed = [{'spec': 'soundfile==0.14.0', 'error': 'missing DLL'}]
        with self.assertRaisesRegex(RuntimeError, 'missing DLL'):
            self.run_install(probes=[failed, failed])
        self.assertFalse(any('--fetch-models' in c for c in self.calls))
        self.assertFalse((home / 'ready.json').exists())
        self.assertEqual(self.saved.read_bytes(), b'<previous-active-pack/>')

    def test_dependency_probe_runs_real_child_and_reports_missing_imports(self):
        self.here.stop()
        try:
            result = setup.probe_dependencies(sys.executable, 'cpu', dict(os.environ))
            self.assertIsInstance(result, list)
            self.assertTrue(all('spec' in item and 'error' in item for item in result))
            self.assertFalse(any(item['spec'].startswith('torch==') for item in result))
        finally:
            self.here.start()


if __name__ == '__main__':
    unittest.main(verbosity=2)
