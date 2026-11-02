"""Regression tests for Windows WinError 1314 using the actual pinned HF downloader."""
import concurrent.futures
import hashlib
import http.server
import os
from pathlib import Path
import tempfile
import subprocess
import sys
import threading
import unittest
from unittest.mock import patch

import huggingface_hub.file_download as hub
from huggingface_hub import hf_hub_download
from hf_cache import configure_copy_cache, copy_blob, comparison_path
from setup_vi import staging_home, space_required

ORIGINAL_LINK = hub._create_symlink
ORIGINAL_SUPPORT = hub.are_symlinks_supported


def no_privilege(*args, **kwargs):
    error = OSError(1314, 'A required privilege is not held by the client')
    error.winerror = 1314
    raise error


class CacheTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='v15 cache Tiếng Việt ')
        self.home = Path(self.temp.name)
        self.root = self.home / 'hf' / 'hub'
        self.source = self.root / 'models--v15--fixture' / 'blobs' / 'source'
        self.destination = self.source.parent.parent / 'snapshots' / ('a' * 40) / 'config.json'
        self.source.parent.mkdir(parents=True)
        self.destination.parent.mkdir(parents=True)
        self.source.write_bytes(b'{"model":"Vietnamese"}')

    def tearDown(self):
        hub._create_symlink = ORIGINAL_LINK
        hub.are_symlinks_supported = ORIGINAL_SUPPORT
        self.temp.cleanup()

    def test_reproduce_1314_then_fix_same_blob(self):
        with patch.object(hub, 'are_symlinks_supported', return_value=True), patch('os.symlink', side_effect=no_privilege):
            with self.assertRaises(OSError) as error:
                ORIGINAL_LINK(str(self.source), str(self.destination), new_blob=True)
            self.assertEqual(error.exception.winerror, 1314)
        configure_copy_cache(self.home / 'hf')
        with patch('os.symlink', side_effect=no_privilege) as symlink:
            hub._create_symlink(str(self.source), str(self.destination), new_blob=True)
        self.assertEqual(symlink.call_count, 0)
        self.assertEqual(self.destination.read_bytes(), self.source.read_bytes())
        self.assertFalse(self.destination.is_symlink())
        self.assertTrue(self.source.exists())

    def test_failed_copy_keeps_blob_and_previous_snapshot(self):
        self.destination.write_bytes(b'previous valid snapshot')
        with patch('os.replace', side_effect=OSError('disk write failed')):
            with self.assertRaises(OSError):
                copy_blob(self.source, self.destination, cache_root=self.root)
        self.assertEqual(self.destination.read_bytes(), b'previous valid snapshot')
        self.assertEqual(self.source.read_bytes(), b'{"model":"Vietnamese"}')
        self.assertEqual(list(self.destination.parent.glob('.v15-copy-*')), [])

    def test_parallel_snapshots_preserve_shared_blob(self):
        paths = [self.destination.parent / ('file-' + str(i)) for i in range(6)]
        with patch('os.symlink', side_effect=no_privilege):
            with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
                list(pool.map(lambda p: copy_blob(self.source, p, cache_root=self.root), paths))
        self.assertTrue(self.source.exists())
        self.assertTrue(all(p.read_bytes() == self.source.read_bytes() and not p.is_symlink() for p in paths))

    def test_cache_path_boundary(self):
        with self.assertRaises(ValueError):
            copy_blob(self.source, self.home / 'outside', cache_root=self.root)
        foreign = self.home / 'foreign'; foreign.write_bytes(b'private')
        with self.assertRaises(ValueError):
            copy_blob(foreign, self.destination, cache_root=self.root)
        self.assertFalse((self.home / 'outside').exists())

    def test_windows_extended_path_spelling(self):
        self.assertEqual(comparison_path('\\\\?\\C:\\V15\\model'), Path('C:\\V15\\model'))
        self.assertEqual(comparison_path('\\\\?\\UNC\\server\\share\\model'), Path('\\\\server\\share\\model'))

    def test_resume_selects_recent_incomplete_and_ignores_active(self):
        old = self.home / 'vieneu-3.8.3-gpu-z-old'
        recent = self.home / 'vieneu-3.8.3-gpu-a-recent'
        active = self.home / 'vieneu-3.8.3-gpu-active'
        for index, p in enumerate([old, recent, active]):
            p.mkdir(); (p / '.incomplete').touch(); os.utime(p / '.incomplete', (100 + index, 100 + index))
        (recent / 'downloaded-model').write_bytes(b'preserve')
        (active / 'ready.json').write_text('{}')
        self.assertEqual(staging_home(self.home, 'gpu'), recent)
        self.assertEqual((recent / 'downloaded-model').read_bytes(), b'preserve')
        self.assertNotEqual(staging_home(self.home, 'cpu'), recent)

    def test_retry_space_budget_accounts_for_existing_stage(self):
        with patch('os.walk', return_value=[(str(self.source.parent), [], [self.source.name])]):
            self.assertEqual(space_required(self.home, 'gpu'), 12 * 1024**3 - self.source.stat().st_size)
        self.assertEqual(space_required(self.home / 'not-created', 'cpu'), 5 * 1024**3)

    def test_offline_flags_precede_hub_import_in_isolated_worker(self):
        script = Path(__file__).with_name('vi_worker.py')
        code = "import runpy,sys; scope=runpy.run_path(sys.argv[1]); scope['environment'](sys.argv[2]); from huggingface_hub import constants; assert constants.HF_HUB_OFFLINE; print('OFFLINE_OK')"
        env = dict(os.environ)
        env.pop('HF_HUB_OFFLINE', None)
        env.pop('TRANSFORMERS_OFFLINE', None)
        result = subprocess.run([sys.executable, '-I', '-c', code, str(script.resolve()), str(self.home)], env=env, capture_output=True, text=True, timeout=15)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('OFFLINE_OK', result.stdout)

    def test_real_downloader_fresh_resume_offline_without_symlink(self):
        body = b'{"model":"cache regression fixture"}'
        etag = hashlib.sha256(body).hexdigest()
        calls = {'get': 0, 'head': 0}
        class Handler(http.server.BaseHTTPRequestHandler):
            def do_HEAD(self):
                calls['head'] += 1
                self.send_response(200)
                self.send_header('X-Repo-Commit', 'b' * 40)
                self.send_header('ETag', '"' + etag + '"')
                self.send_header('Content-Length', str(len(body)))
                self.end_headers()
            def do_GET(self):
                calls['get'] += 1
                self.send_response(200)
                self.send_header('Content-Length', str(len(body)))
                self.end_headers(); self.wfile.write(body)
            def log_message(self, *args):
                pass
        server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
        cache = str(self.root)
        endpoint = 'http://127.0.0.1:' + str(server.server_port)
        kwargs = dict(repo_id='v15/fixture-http', filename='config.json', cache_dir=cache, endpoint=endpoint)
        try:
            configure_copy_cache(self.home / 'hf')
            with patch('os.symlink', side_effect=no_privilege) as denied:
                first = Path(hf_hub_download(**kwargs))
                self.assertEqual(first.read_bytes(), body)
                self.assertFalse(first.is_symlink())
                self.assertEqual(calls['get'], 1)
                # Reproduce an interrupted install after blob completion, before pointer creation.
                first.unlink()
                second = Path(hf_hub_download(**kwargs))
                self.assertEqual(second.read_bytes(), body)
                self.assertEqual(calls['get'], 1, 'Cached blob must avoid another GET')
                self.assertEqual(denied.call_count, 0)
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)
        with patch('requests.Session.request', side_effect=AssertionError('Offline cache tried network')):
            third = Path(hf_hub_download(**kwargs, local_files_only=True))
            self.assertEqual(third.read_bytes(), body)


if __name__ == '__main__':
    unittest.main(verbosity=2)
