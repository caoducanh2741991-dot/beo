"""V15 Vietnamese worker. Inference is local-only; setup is a separate process."""
import argparse
import base64
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import unicodedata
import wave

sys.path.insert(0, str(Path(__file__).resolve().parent))


def emit(kind, message):
    print('V15_' + kind + '\t' + base64.b64encode(str(message).encode('utf-8')).decode('ascii'), flush=True)


def normalize(text):
    text = unicodedata.normalize('NFC', text).replace('\u00a0', ' ')
    if any(ord(c) < 32 and c not in '\n\r\t' for c in text):
        raise ValueError('Văn bản có ký tự điều khiển không hợp lệ.')
    # Number/date/pronunciation normalization belongs to the pinned sea-g2p SDK.
    return ' '.join(text.split())


def environment(home, offline=True):
    os.environ['HF_HOME'] = str(Path(home) / 'hf')
    os.environ['HF_HUB_DISABLE_TELEMETRY'] = '1'
    os.environ['HF_HUB_DISABLE_XET'] = '1'
    os.environ['DO_NOT_TRACK'] = '1'
    os.environ['NUMBA_CACHE_DIR'] = str(Path(home) / 'numba')
    if offline:
        os.environ['HF_HUB_OFFLINE'] = '1'
        os.environ['TRANSFORMERS_OFFLINE'] = '1'
        def no_network(event, args):
            if event in ('socket.connect', 'socket.getaddrinfo'):
                raise RuntimeError('Đang chạy offline; model còn thiếu. Hãy chạy lại Cài gói tiếng Việt.')
        sys.addaudithook(no_network)
    # Hugging Face reads offline/cache settings at import time.
    from hf_cache import configure_copy_cache
    configure_copy_cache(Path(home) / 'hf')


def device_policy(requested, torch_module=None):
    if requested == 'cpu':
        return 'cpu', 'float32', 'CPU · VieNeu ONNX FP32'
    if torch_module is None:
        try:
            import torch as torch_module
        except (ImportError, OSError, RuntimeError) as exc:
            if requested == 'gpu':
                raise RuntimeError('Không nạp được CUDA tiếng Việt. Cài gói GPU tiếng Việt trong Công cụ & thiết lập. ' + str(exc)) from exc
            return 'cpu', 'float32', 'CPU · VieNeu ONNX FP32'
    if not torch_module.cuda.is_available():
        if requested == 'gpu':
            raise RuntimeError('CUDA tiếng Việt chưa hoạt động. Kiểm tra driver NVIDIA hoặc chọn Auto/CPU.')
        return 'cpu', 'float32', 'CPU · VieNeu ONNX FP32'
    # Turing (RTX 2070, sm_75) does not have native BF16. Force FP32, no FlashAttention.
    return 'cuda', 'float32', 'NVIDIA GPU · ' + torch_module.cuda.get_device_name(0) + ' · VieNeu FP32'


def load(home, requested, threads, reference='', speaker='Hải Đăng', offline=True):
    environment(home, offline)
    from vieneu import Vieneu
    device, dtype, label = device_policy(requested)
    emit('STATUS', 'Đang nạp model tiếng Việt · ' + label)
    try:
        tts = Vieneu(mode='v3turbo', device=device, dtype=dtype,
                     backend='pytorch' if device == 'cuda' else 'onnx',
                     precision='fp32', threads=threads, max_batch_size=1, max_streams=1)
        if reference:
            with wave.open(reference, 'rb') as w:
                seconds = w.getnframes() / w.getframerate()
            if not 3 <= seconds <= 8.1:
                raise ValueError('Giọng Việt cần đoạn mẫu dài 3–8 giây. Hãy tạo lại giọng với mẫu 6–8 giây.')
            emit('STATUS', 'Đang lấy đặc điểm giọng Việt; dùng lại cho các câu tiếp theo')
            speaker = '__v15_reference_' + hashlib.sha256(Path(reference).read_bytes()).hexdigest()[:16]
            tts.add_voice(speaker, reference, denoise=True, save=False)
        elif tts.resolve_voice_name(speaker) is None:
            raise ValueError('Không tìm thấy giọng Việt trong SDK đã cài: ' + speaker)
    except Exception as exc:
        if device == 'cuda' and requested == 'auto':
            raise RuntimeError('GPU_FALLBACK: ' + str(exc)) from exc
        raise
    return tts, speaker, label, device


def synthesize(tts, speaker, text, speed, output, ffmpeg, work):
    import numpy as np
    import soundfile as sf
    if not math.isfinite(speed) or not .5 <= speed <= 1.6:
        raise ValueError('Tốc độ tiếng Việt phải trong khoảng 0.50–1.60.')
    text = normalize(text)
    if not text or len(text) > 20000:
        raise ValueError('Đoạn lời đọc trống hoặc vượt 20.000 ký tự.')
    audio = np.asarray(tts.infer(text, voice=speaker, temperature=.8, batch_size=1), dtype=np.float32)
    if audio.ndim != 1 or not audio.size or not np.isfinite(audio).all():
        raise ValueError('Model tiếng Việt trả về âm thanh rỗng hoặc không hợp lệ.')
    rms = float(np.sqrt(np.mean(audio.astype(np.float64) ** 2)))
    if rms < .0001 or len(audio) / 48000 > max(90, len(text.split()) * 3):
        raise ValueError('Âm thanh tiếng Việt im lặng hoặc dài bất thường; hãy thử tạo lại câu.')
    raw = Path(work) / 'vi_source.wav'
    temp = Path(work) / 'vi_pcm.wav'
    sf.write(str(raw), audio, 48000, subtype='FLOAT')
    try:
        command = [str(ffmpeg), '-v', 'error', '-y', '-i', str(raw)]
        if abs(speed - 1) > .00001:
            command += ['-af', 'atempo=' + format(speed, '.8f')]
        command += ['-ac', '1', '-ar', '24000', '-c:a', 'pcm_s16le', str(temp)]
        subprocess.run(command, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=120)
        with wave.open(str(temp), 'rb') as w:
            if w.getframerate() != 24000 or w.getnchannels() != 1 or w.getnframes() < 1200:
                raise ValueError('WAV tiếng Việt không hợp lệ.')
        # The C# owner passes an existing per-render directory. Never publish partial WAV.
        Path(output).write_bytes(temp.read_bytes())
    finally:
        raw.unlink(missing_ok=True)
        temp.unlink(missing_ok=True)


def selftest(home, device, ffmpeg, offline=True):
    import tempfile
    with tempfile.TemporaryDirectory(prefix='v15_vi_test_') as work:
        tts, speaker, label, _ = load(home, device, 4, offline=offline)
        out = Path(work) / 'preset.wav'
        synthesize(tts, speaker, 'Xin chào bạn. Đây là phần kiểm tra giọng nói tiếng Việt trên máy tính.', 1, out, ffmpeg, work)
        # Exercise the lazy cloning dependencies too; preset success alone is insufficient.
        ref = Path(work) / 'ref.wav'
        with wave.open(str(out), 'rb') as w:
            frames = w.readframes(w.getnframes())
        duration = len(frames) / 48000
        if duration < 3:
            raise RuntimeError('Mẫu tự kiểm tra quá ngắn để kiểm tra sao chép giọng.')
        with wave.open(str(ref), 'wb') as w:
            w.setparams((1, 2, 24000, 0, 'NONE', 'not compressed'))
            w.writeframes(frames[:min(len(frames), 7 * 48000)])
        tts.add_voice('__v15_setup_clone', str(ref), denoise=True, save=False)
        synthesize(tts, '__v15_setup_clone', 'Giọng sao chép đã sẵn sàng để đọc kịch bản tiếng Việt.', 1, Path(work) / 'clone.wav', ffmpeg, work)
        emit('STATUS', ('Đã thử giọng có sẵn và sao chép giọng khi chặn mạng · ' if offline else 'Đã nạp đủ thành phần đọc và sao chép; tiếp tục kiểm tra offline · ') + label)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--home', required=True)
    parser.add_argument('--ffmpeg', required=True)
    parser.add_argument('--device', choices=['auto', 'gpu', 'cpu'], default='auto')
    parser.add_argument('--speaker', default='Hải Đăng')
    parser.add_argument('--reference', default='')
    parser.add_argument('--threads', type=int, default=4)
    parser.add_argument('--work-dir')
    parser.add_argument('--seed-offset', type=int, default=0)
    parser.add_argument('--model', default='vieneu_v3')
    parser.add_argument('--ref-text', default='')
    parser.add_argument('--selftest', action='store_true')
    parser.add_argument('--warmup', action='store_true')
    args = parser.parse_args()
    try:
        if args.selftest or args.warmup:
            selftest(args.home, args.device, args.ffmpeg, offline=not args.warmup)
            return 0
        work = Path(args.work_dir)
        if not work.is_dir():
            raise ValueError('Thiếu thư mục tác vụ tiếng Việt.')
        tts, speaker, label, device = load(args.home, args.device, args.threads, args.reference, args.speaker)
        emit('READY', label)
        for line in sys.stdin:
            if line.strip() == 'QUIT':
                break
            try:
                command, encoded_text, encoded_output, speed = line.rstrip('\r\n').split('\t')
                if command != 'SYNTH':
                    raise ValueError('Lệnh tiếng Việt không hợp lệ.')
                text = base64.b64decode(encoded_text, validate=True).decode('utf-8')
                output = base64.b64decode(encoded_output, validate=True).decode('utf-8')
                emit('STATUS', 'Đang tạo giọng tiếng Việt · ' + label)
                synthesize(tts, speaker, text, float(speed), output, args.ffmpeg, work)
                emit('OK', label)
            except Exception as exc:
                emit('ERR', ('GPU_FALLBACK: ' if device == 'cuda' and args.device == 'auto' else '') + str(exc))
    except Exception as exc:
        emit('ERR', exc)
        return 2
    return 0


if __name__ == '__main__':
    sys.exit(main())
