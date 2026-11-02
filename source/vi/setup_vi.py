"""Transactional, per-user Vietnamese pack installer; leaves English runtime untouched."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))  # Windows embedded Python has an isolated ._pth.


def status(message):
    print('V15_STATUS\t' + base64.b64encode(message.encode('utf-8')).decode('ascii'), flush=True)


def digest(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def download(url, target, expected):
    if target.is_file() and digest(target) == expected:
        return target
    target.parent.mkdir(parents=True, exist_ok=True)
    partial = target.with_suffix(target.suffix + '.part')
    error = None
    for attempt in range(3):
        try:
            with urllib.request.urlopen(url, timeout=90) as response, open(partial, 'wb') as f:
                shutil.copyfileobj(response, f, 1024 * 1024)
            if digest(partial) != expected:
                raise ValueError('Tệp tải về sai SHA-256: ' + target.name)
            os.replace(partial, target)
            return target
        except Exception as exc:
            error = exc
            partial.unlink(missing_ok=True)
    raise RuntimeError('Không tải được ' + target.name + ': ' + str(error))


def extract(archive, folder):
    with zipfile.ZipFile(archive) as z:
        for info in z.infolist():
            target = (folder / info.filename).resolve()
            if not target.is_relative_to(folder.resolve()) or info.filename.startswith(('/', '\\')):
                raise ValueError('Đường dẫn ZIP không hợp lệ.')
        z.extractall(folder)


def run(command, env, timeout=10800):
    # Parent app captures stderr to a bounded log. Output never mixes with the protocol.
    subprocess.run([str(x) for x in command], env=env, check=True, timeout=timeout, stdout=sys.stderr)


def fetch_models(home, mode):
    # Called by the NEW runtime, never importing packages into the English runtime.
    from huggingface_hub import snapshot_download
    spec = json.loads((HERE / 'sdk_lock.json').read_text())
    repos = [('pnnbao-ump/VieNeu-TTS-v3-Turbo', spec['model_revision'],
              ['config.json', 'speaker_encoder.onnx', 'denoiser.onnx', 'onnx_update/*'] + (['update/*'] if mode == 'gpu' else [])),
             ('OpenMOSS-Team/MOSS-Audio-Tokenizer-Nano-ONNX', spec['onnx_codec_revision'], ['*.json', '*.onnx', '*.data'])]
    if mode == 'gpu':
        repos.append(('OpenMOSS-Team/MOSS-Audio-Tokenizer-Nano', spec['codec_revision'], ['*.py', '*.json', '*.safetensors']))
    for repo, revision, patterns in repos:
        status('Đang tải model ' + repo + ' · chỉ cần tải một lần')
        snapshot_download(repo, revision=revision, allow_patterns=patterns, max_workers=3)
        # SDK requests main. Resolve it to the exact snapshot validated with this release.
        ref = home / 'hf' / 'hub' / ('models--' + repo.replace('/', '--')) / 'refs' / 'main'
        ref.parent.mkdir(parents=True, exist_ok=True)
        ref.write_text(revision)


def activate(folder, home, mode):
    # Ready and active are published only after BOTH offline preset/clone selftests pass.
    record = {'sdk': '3.8.3', 'mode': mode, 'validated_offline': True}
    (home / 'ready.json').write_text(json.dumps(record), encoding='utf-8')
    active = folder / 'active.xml'
    temp = folder / ('active.' + uuid.uuid4().hex + '.tmp')
    ET.ElementTree(ET.Element('vietnamese', sdk='3.8.3', folder=home.name, mode=mode)).write(temp, encoding='utf-8', xml_declaration=True)
    os.replace(temp, active)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--folder', required=True)
    parser.add_argument('--mode', choices=['gpu', 'cpu'], default='gpu')
    parser.add_argument('--ffmpeg', required=True)
    parser.add_argument('--fetch-models', action='store_true')
    args = parser.parse_args()
    folder = Path(args.folder).resolve()
    if args.fetch_models:
        from vi_worker import environment
        environment(folder, offline=False)
        fetch_models(folder, args.mode)
        return
    if os.name != 'nt':
        raise RuntimeError('Bộ cài tự động này dành cho Windows x64.')
    if not Path(args.ffmpeg).is_file():
        raise FileNotFoundError('Thiếu FFmpeg trong bộ V15 hiện tại.')
    folder.mkdir(parents=True, exist_ok=True)
    lock_file = folder / 'install.lock'
    # OS file lock is released on cancellation/crash; no permanent stale-lock state.
    import msvcrt
    with open(lock_file, 'a+b') as lock:
        lock.seek(0); lock.write(b'0'); lock.flush(); lock.seek(0)
        try:
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        except OSError as exc:
            raise RuntimeError('Đang có một lần cài gói Việt khác. Chờ tác vụ đó hoàn tất.') from exc
        spec = json.loads((HERE / 'sdk_lock.json').read_text())
        cache = folder / 'downloads'
        incomplete = sorted(p for p in folder.glob('vieneu-3.8.3-' + args.mode + '-*')
                            if (p / '.incomplete').is_file() and not (p / 'ready.json').exists())
        home = incomplete[-1] if incomplete else folder / ('vieneu-3.8.3-' + args.mode + '-' + uuid.uuid4().hex[:10])
        runtime = home / 'runtime'
        runtime.mkdir(parents=True, exist_ok=True)
        (home / '.incomplete').touch()
        env = dict(os.environ, PYTHONIOENCODING='utf-8', HF_HOME=str(home / 'hf'), HF_HUB_DISABLE_XET='1',
                   HF_HUB_DISABLE_TELEMETRY='1', DO_NOT_TRACK='1', PIP_CACHE_DIR=str(folder / 'pip-cache'))
        for name in ('PYTHONHOME', 'PYTHONPATH', 'HF_HUB_OFFLINE', 'TRANSFORMERS_OFFLINE'):
            env.pop(name, None)
        success = False
        try:
            if shutil.disk_usage(folder).free < (12 if args.mode == 'gpu' else 5) * 1024**3:
                raise RuntimeError('Cần ít nhất ' + ('12' if args.mode == 'gpu' else '5') + ' GB trống để tải và cài gói Việt.')
            status('Đang tạo Python riêng cho tiếng Việt · không thay bộ tiếng Anh')
            embed = download(spec['python_url'], cache / 'python-3.12.10-embed-amd64.zip', spec['python_sha256'])
            extract(embed, runtime)
            (runtime / 'python312._pth').write_text('python312.zip\n.\nLib/site-packages\nimport site\n')
            packages = runtime / 'Lib' / 'site-packages'
            packages.mkdir(parents=True)
            pip_wheel = download(spec['pip_url'], cache / 'pip.whl', spec['pip_sha256'])
            extract(pip_wheel, packages)
            python = runtime / 'python.exe'
            sdk_wheel = download(spec['url'], cache / spec['filename'], spec['sha256'])
            status('Đang cài thư viện tiếng Việt · lần đầu có thể mất nhiều phút')
            common = [python, '-m', 'pip', 'install', '--disable-pip-version-check', '--only-binary=:all:']
            run(common + ['-c', HERE / 'constraints-vi.txt', '-r', HERE / 'requirements-vi.txt'], env)
            run(common + ['--no-deps', sdk_wheel], env)
            if args.mode == 'gpu':
                status('Đang tải CUDA riêng cho tiếng Việt · gói lớn, giữ cửa sổ mở')
                run(common + ['torch==2.8.0', 'torchaudio==2.8.0', '--index-url', 'https://download.pytorch.org/whl/cu128'], env)
                run(common + ['transformers==4.57.6', 'safetensors==0.6.2', 'einops==0.8.1', 'scipy==1.18.1'], env)
            run([python, HERE / 'setup_vi.py', '--folder', home, '--mode', args.mode, '--ffmpeg', args.ffmpeg, '--fetch-models'], env)
            status('Nạp trước giọng và bộ sao chép để tải đủ thành phần')
            worker = [python, HERE / 'vi_worker.py', '--home', home, '--ffmpeg', args.ffmpeg]
            run(worker + ['--device', args.mode, '--warmup'], env)
            status('Đang kiểm tra đọc và sao chép khi chặn kết nối mạng')
            run(worker + ['--device', args.mode, '--selftest'], env)
            if args.mode == 'gpu':
                # Auto must be able to fall back to the same pack's CPU graphs.
                run(worker + ['--device', 'cpu', '--selftest'], env)
            run([python, '-m', 'pip', 'freeze'], env)
            activate(folder, home, args.mode)
            (home / '.incomplete').unlink(missing_ok=True)
            success = True
            print('V15_SETUP_OK\t' + base64.b64encode(('Gói tiếng Việt đã cài · ' + args.mode.upper() + ' · thử offline thành công').encode()).decode(), flush=True)
        finally:
            if not success:
                # Reuse the staging runtime/model cache after cancellation/network failure.
                # No active pointer is published, and the completed pack is never touched.
                status('Cài chưa hoàn tất; giữ dữ liệu tải đệm để lần sau tiếp tục. Gói đang dùng không thay đổi.')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr, flush=True)
        sys.exit(2)
