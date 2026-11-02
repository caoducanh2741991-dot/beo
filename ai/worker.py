"""Offline-only speech worker for V15. All weights and libraries are bundled."""
import argparse
import base64
import contextlib
import hashlib
import json
import math
import os
import re
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import traceback

PROTOCOL_OUT = sys.stdout
ROOT = Path(__file__).resolve().parent
# Embedded Python on Windows uses an isolated ._pth; add our own module directory explicitly.
sys.path.insert(0, str(ROOT))
os.environ.update(HF_HUB_OFFLINE="1", TRANSFORMERS_OFFLINE="1", HF_HUB_DISABLE_TELEMETRY="1",
                  TOKENIZERS_PARALLELISM="false", DO_NOT_TRACK="1")
os.environ.setdefault("NUMBA_CACHE_DIR", str(Path(tempfile.gettempdir()) / "V15_numba"))
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "max_split_size_mb:128")
os.environ.setdefault("OMP_NUM_THREADS", "4")
os.environ.setdefault("MKL_NUM_THREADS", "4")


def no_network(*args, **kwargs):
    raise OSError("V15 speech worker runs offline; network access is disabled")


socket.create_connection = no_network
socket.socket.connect = no_network
socket.socket.connect_ex = no_network


def dec(s):
    return base64.b64decode(s, validate=True).decode("utf-8")


def emit(kind, value=""):
    print("V15_" + kind + "\t" + base64.b64encode(value.encode("utf-8")).decode("ascii"), file=PROTOCOL_OUT, flush=True)


def split_text(text):
    segments = []
    current = []
    for word in text.split():
        current.append(word)
        if len(current) >= 24 or (len(current) >= 4 and re.search(r'[.!?][\"”’)\]]?$', word)):
            segments.append(" ".join(current))
            current = []
    if current:
        segments.append(" ".join(current))
    return segments


class BadSpeech(ValueError):
    """Invalid generation that can be retried; never publish its waveform."""


def speech_limits(text):
    words = max(1, len(text.split()))
    seconds = max(10., words * .8 + 4., len(text) * .075 + 3.)
    return seconds, min(768, max(96, math.ceil(seconds * 12.5) + 16))


def validate_speech(part, sr, text, cap, np):
    if not isinstance(sr, (int, float)) or not math.isfinite(sr) or not 8000 <= sr <= 96000:
        raise BadSpeech("Model trả tần số âm thanh không hợp lệ")
    audio = np.asarray(part, dtype=np.float32).reshape(-1)
    if not len(audio) or not np.isfinite(audio).all():
        raise BadSpeech("Model trả âm thanh trống hoặc chứa NaN/Inf")
    seconds = len(audio) / sr
    limit, _ = speech_limits(text)
    # A response at the token cap may be incomplete. Retry instead of accepting a clipped sentence.
    if seconds >= limit or (cap and seconds >= (cap - 4) / 12.5):
        raise BadSpeech("Âm thanh dài bất thường hoặc chạm giới hạn sinh (%.2fs)" % seconds)
    if seconds < .10 or float(np.max(np.abs(audio))) < .0001:
        raise BadSpeech("Model trả đoạn quá ngắn hoặc im lặng")
    return audio


def synthesize_segment(model, kind, speaker, prompt, text, torch, np, device, seed_offset, report):
    last = ""
    for attempt in range(3):
        report("Sinh cụm lời · lần thử %d/3" % (attempt + 1))
        seed = int.from_bytes(hashlib.sha256((str(seed_offset) + ':' + str(attempt) + ':' + text).encode()).digest()[:4], 'little')
        torch.manual_seed(seed)
        np.random.seed(seed)
        _, cap = speech_limits(text)
        deadline = time.monotonic() + (max(45., len(text.split()) * 4. + 20.) if device.startswith('cuda') else max(90., len(text.split()) * 12. + 45.))
        try:
            if kind == 'chatter_nano':
                part = model.generate(text).detach().cpu().numpy().reshape(-1)
                sr = model.sr
                cap = None
            else:
                from transformers import StoppingCriteriaList
                class Deadline:
                    def __call__(self, input_ids, scores, **kwargs):
                        if time.monotonic() > deadline:
                            raise BadSpeech("Quá thời gian sinh cụm lời; đang thử cấu hình khác")
                        return False
                settings = dict(do_sample=True, top_k=50, top_p=1., temperature=(.9, .8, .7)[attempt],
                    repetition_penalty=(1.05, 1.10, 1.15)[attempt], subtalker_dosample=True,
                    subtalker_top_k=50, subtalker_top_p=1., subtalker_temperature=(.9, .8, .7)[attempt],
                    max_new_tokens=cap, stopping_criteria=StoppingCriteriaList([Deadline()]))
                # qwen-tts 0.1.1 drops arbitrary kwargs before talker.generate().
                # Install the deadline at that actual generation boundary, then always restore it.
                talker = model.model.talker
                original_generate = talker.generate
                def guarded_generate(*args, **kwargs):
                    existing = list(kwargs.get('stopping_criteria') or [])
                    kwargs['stopping_criteria'] = StoppingCriteriaList(existing + [Deadline()])
                    return original_generate(*args, **kwargs)
                talker.generate = guarded_generate
                try:
                    if kind == 'qwen_custom':
                        waves, sr = model.generate_custom_voice(text=text, language='English', speaker=speaker, **settings)
                    else:
                        waves, sr = model.generate_voice_clone(text=text, language='English', voice_clone_prompt=prompt, **settings)
                finally:
                    talker.generate = original_generate
                if not waves:
                    raise BadSpeech("Model không trả âm thanh")
                part = waves[0]
            return validate_speech(part, sr, text, cap, np), sr
        except BadSpeech as exc:
            last = str(exc)
            report("Kết quả chưa hợp lệ · lần %d/3: %s" % (attempt + 1, last))
    raise BadSpeech("SYNTH_INVALID: Đã thử 3 lần nhưng chưa có âm thanh hợp lệ. " + last)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--model", choices=["qwen_custom", "qwen_base", "chatter_nano"], default="qwen_custom")
    p.add_argument("--speaker", default="Aiden")
    p.add_argument("--reference", default="")
    p.add_argument("--ref-text", default="")
    p.add_argument("--work-dir", default=None)
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--device", choices=["auto", "gpu", "cpu"], default="auto")
    p.add_argument("--probe", action="store_true")
    p.add_argument("--seed-offset", type=int, default=0)
    a = p.parse_args()
    from gpu_policy import runtime_path, select_device, is_gpu_error
    sys.path.insert(0, str(ROOT / "vendor" / ("chatter" if a.model == "chatter_nano" else "qwen")))
    gpu_installed = runtime_path(ROOT, a.device)
    if a.device == "gpu" and os.name == "nt" and not gpu_installed:
        raise RuntimeError("Chưa cài gói GPU CUDA 12.4. Vào Cập nhật từ ZIP để thêm gói GPU.")
    # Keep protocol on stdout; upstream progress and model messages go to stderr.
    with contextlib.redirect_stdout(sys.stderr):
        import numpy as np
        import soundfile as sf
        import torch
        device, device_info = select_device(torch, a.device)
        if a.probe:
            if device.startswith("cuda"):
                torch.ones(8, device=device).sum().item()
                torch.cuda.synchronize()
                device_info += " · kiểm tra tính toán CUDA đạt"
            emit("PROBE", device_info)
            return
        torch.set_num_threads(max(1, min(8, a.threads)))
        torch.set_num_interop_threads(1)
        if a.reference:
            info = sf.info(a.reference)
            if not 3 <= info.duration <= 30:
                raise ValueError("Mẫu tham chiếu cần dài 3–30 giây; nên chọn 10–15 giây lời nói rõ")
            reference, _ = sf.read(a.reference, dtype='float32')
            if not np.isfinite(reference).all() or not reference.size or float(np.max(np.abs(reference))) < .0001:
                raise ValueError("Mẫu tham chiếu trống, im lặng hoặc chứa dữ liệu không hợp lệ")
        emit('STATUS', 'Đang nạp model và mẫu giọng · ' + device_info)
        prompt = None
        if a.model == "chatter_nano":
            from chatterbox.tts_turbo import ChatterboxTurboTTS
            model = ChatterboxTurboTTS.from_local(ROOT / "models" / a.model, device, nano=True)
            if a.reference:
                model.prepare_conditionals(a.reference)
            sr = model.sr
        else:
            from qwen_tts import Qwen3TTSModel
            model = Qwen3TTSModel.from_pretrained(str(ROOT / "models" / a.model), device_map=device,
                dtype=torch.float32, attn_implementation="sdpa", local_files_only=True)
            prompt = None
            if a.model == "qwen_base":
                if not a.reference:
                    raise ValueError("A reference audio clip is required")
                prompt = model.create_voice_clone_prompt(ref_audio=a.reference, ref_text=a.ref_text or None,
                                                        x_vector_only_mode=not bool(a.ref_text.strip()))
    emit("READY", device_info)
    with tempfile.TemporaryDirectory(prefix="V15_ai_", dir=a.work_dir) as tmp:
        raw = Path(tmp) / "natural.wav"
        previous = None
        for line in sys.stdin:
            try:
                parts = line.rstrip("\r\n").split("\t")
                if parts == ["QUIT"]:
                    break
                if len(parts) != 4 or parts[0] != "SYNTH":
                    raise ValueError("Invalid speech request")
                text, output, speed = dec(parts[1]), dec(parts[2]), float(parts[3])
                if not text.strip() or len(text) > 6000 or not .5 <= speed <= 2:
                    raise ValueError("Invalid speech text or speed")
                with contextlib.redirect_stdout(sys.stderr), torch.inference_mode():
                    if text != previous:
                        pieces = []
                        segments = split_text(text)
                        for index, segment in enumerate(segments):
                            def report(message):
                                emit('STATUS', 'Cụm %d/%d · %s · %s' % (index + 1, len(segments), message, device_info))
                            part, sr = synthesize_segment(model, a.model, a.speaker, prompt, segment,
                                torch, np, device, a.seed_offset, report)
                            if pieces:
                                pieces.append(np.zeros(int(sr * .10), dtype=np.float32))
                            pieces.append(part)
                        audio = np.concatenate(pieces)
                        if not len(audio) or not np.isfinite(audio).all():
                            raise ValueError("Model returned empty or invalid audio")
                        if len(audio) / sr > 120:
                            raise ValueError("Speech exceeded the segment limit; split the text")
                        pending = Path(tmp) / 'natural.pending.wav'
                        sf.write(pending, audio, sr, subtype="PCM_16")
                        os.replace(pending, raw)
                        previous = text
                    else:
                        emit('STATUS', 'Dùng lại âm thanh hợp lệ để đổi tốc độ · ' + device_info)
                ffmpeg = str(ROOT / "ffmpeg.exe") if os.name == "nt" else "ffmpeg"
                # atempo changes duration while preserving pitch; retries reuse the same speech.
                flags = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
                converted = str(Path(output).with_name('.' + Path(output).name + '.v15.tmp.wav'))
                emit('STATUS', 'Đang kiểm tra và chuẩn hóa WAV · ' + device_info)
                try:
                    r = subprocess.run([ffmpeg, "-nostdin", "-v", "error", "-y", "-i", str(raw),
                        "-af", "atempo=" + str(speed), "-ac", "1", "-ar", "24000", "-c:a", "pcm_s16le", converted],
                        stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120, **flags)
                    if r.returncode:
                        raise RuntimeError(r.stderr.decode("utf-8", errors="replace")[-2000:])
                    info = sf.info(converted)
                    if info.frames < 1 or info.samplerate != 24000 or info.channels != 1 or info.subtype != 'PCM_16':
                        raise ValueError('WAV đầu ra không hợp lệ')
                    os.replace(converted, output)
                finally:
                    Path(converted).unlink(missing_ok=True)
                if device.startswith("cuda"):
                    free, total = torch.cuda.mem_get_info(0)
                    device_info = "%s · VRAM dùng %.1f / %.1f GB" % (torch.cuda.get_device_name(0), (total-free)/2**30, total/2**30)
                emit("OK", device_info)
            except Exception as exc:
                traceback.print_exc(file=sys.stderr)
                emit("ERR", ("GPU_FALLBACK: " if device.startswith("cuda") and is_gpu_error(exc) else "") + str(exc))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        traceback.print_exc(file=sys.stderr)
        from gpu_policy import is_gpu_error
        emit("ERR", ("GPU_FALLBACK: " if is_gpu_error(exc) else "") + str(exc))
        sys.exit(1)
