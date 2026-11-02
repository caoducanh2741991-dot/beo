"""Offline-only speech worker for V15. All weights and libraries are bundled."""
import argparse
import base64
import contextlib
import hashlib
import json
import os
import re
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import traceback

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
    print("V15_" + kind + "\t" + base64.b64encode(value.encode("utf-8")).decode("ascii"), flush=True)


def split_text(text):
    segments = []
    current = []
    for word in text.split():
        current.append(word)
        if len(current) >= 40 or re.search(r'[.!?][\"”’)\]]?$', word):
            segments.append(" ".join(current))
            current = []
    if current:
        segments.append(" ".join(current))
    return segments


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
    if "onnxruntime" in sys.modules:
        raise RuntimeError("Unexpected ONNX Runtime import")
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
                        for segment in split_text(text):
                            seed = int.from_bytes(hashlib.sha256(segment.encode("utf-8")).digest()[:4], "little")
                            torch.manual_seed(seed)
                            np.random.seed(seed)
                            if a.model == "chatter_nano":
                                part = model.generate(segment).detach().cpu().numpy().reshape(-1)
                            elif a.model == "qwen_custom":
                                waves, sr = model.generate_custom_voice(text=segment, language="English", speaker=a.speaker,
                                    do_sample=False, subtalker_dosample=False, max_new_tokens=1536)
                                part = waves[0]
                            else:
                                waves, sr = model.generate_voice_clone(text=segment, language="English", voice_clone_prompt=prompt,
                                    do_sample=False, subtalker_dosample=False, max_new_tokens=1536)
                                part = waves[0]
                            if len(part) / sr > max(18, len(segment.split()) + 6):
                                raise ValueError("Model speech is unexpectedly long; retry or choose another voice")
                            if pieces:
                                pieces.append(np.zeros(int(sr * .10), dtype=np.float32))
                            pieces.append(part)
                        audio = np.concatenate(pieces)
                        if not len(audio) or not np.isfinite(audio).all():
                            raise ValueError("Model returned empty or invalid audio")
                        if len(audio) / sr > 120:
                            raise ValueError("Speech exceeded the segment limit; split the text")
                        sf.write(raw, audio, sr, subtype="PCM_16")
                        previous = text
                ffmpeg = str(ROOT / "ffmpeg.exe") if os.name == "nt" else "ffmpeg"
                # atempo changes duration while preserving pitch; retries reuse the same speech.
                flags = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
                r = subprocess.run([ffmpeg, "-nostdin", "-v", "error", "-y", "-i", str(raw),
                    "-af", "atempo=" + str(speed), "-ac", "1", "-ar", "24000", "-c:a", "pcm_s16le", output],
                    stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120, **flags)
                if r.returncode:
                    raise RuntimeError(r.stderr.decode("utf-8", errors="replace")[-2000:])
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
