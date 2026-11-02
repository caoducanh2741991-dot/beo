"""Align known V15.6 speech against the final fitted WAV, entirely offline.

Unit intervals are search windows only. Published times come from CTC alignment.
Never pass proportional window times off as measured word times.
"""
import argparse
import gc
import math
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path
import asr_worker as worker
from subtitle_core import assemble, write_result


def read_units(path, duration):
    root = ET.parse(path).getroot()
    if root.tag != 'units':
        raise ValueError('Không đúng dữ liệu chặng.')
    segments, stages = [], {}
    previous = 0.0
    for row in root.findall('unit'):
        number = int(row.attrib['stage'])
        start, end, a, b = (float(row.attrib[k]) for k in ('start', 'end', 'stageStart', 'stageEnd'))
        if not all(math.isfinite(n) for n in (start, end, a, b)) or not 0 <= a <= start < end <= b <= duration+.001 or start < previous-.001 or end-start > 38:
            raise ValueError('Cửa sổ căn từ quá dài hoặc không hợp lệ; chia chặng/câu ngắn hơn.')
        bounds = stages.setdefault(number, (a, b))
        if bounds != (a, b) or (len(stages) > 1 and a < list(stages.values())[-2][1]-.001):
            raise ValueError('Mốc chặng chồng nhau.')
        text = (row.text or '').strip()
        if not text:
            raise ValueError('Thiếu lời đọc.')
        segments.append(dict(start=start, end=end, text=text, stage=number))
        previous = end
    if not segments:
        raise ValueError('Không có đơn vị đọc để căn.')
    return segments, stages


def make_output(words, segments, stages, duration):
    grouped = {n: [] for n in stages}
    if ' '.join(w['word'] for w in words).split() != ' '.join(s['text'] for s in segments).split():
        raise ValueError('Căn từ thiếu hoặc thay đổi lời nguồn.')
    for word in words:
        w = dict(word)
        owner = segments[int(w['segment_id'])]['stage']
        a, b = stages[owner]
        start, end = w.get('start'), w.get('end')
        if start is None or end is None or not all(math.isfinite(x) for x in (start, end)) or end <= start:
            raise ValueError('Có từ chưa đo được mốc; chưa xuất SRT theo WAV.')
        if start < a-.20 or end > b+.20:
            raise ValueError('Mốc từ vượt neo chặng; cần nghe và căn lại.')
        w['flags'] = list(w.get('flags', []))
        if start < a or end > b:
            w['flags'].append('Từ sát neo chặng; cần nghe lại')
        w.update(start=max(a, start), end=min(b, end), segment_start=a, segment_end=b)
        if w['end'] <= w['start']:
            raise ValueError('Từ nằm ngoài chặng.')
        grouped[owner].append(w)
    result, measured = [], []
    for number, (a, b) in stages.items():
        chunk = grouped[number]
        cues = assemble(chunk, duration)
        if any(c['start'] < a-.001 or c['end'] > b+.001 for c in cues):
            raise ValueError('Phụ đề vượt mốc neo.')
        result.extend(cues)
        measured.extend(chunk)
    return result, measured


def main():
    parser = argparse.ArgumentParser()
    for key in ('home', 'input', 'draft', 'output', 'ffmpeg'):
        parser.add_argument('--'+key, required=True)
    parser.add_argument('--device', choices=('auto', 'gpu', 'cpu'), default='auto')
    args = parser.parse_args()
    home = Path(args.home)
    if not (home/'models/alignment/wav2vec2_fairseq_base_ls960_asr_ls960.pth').is_file():
        raise RuntimeError('Chưa cài mô hình căn từng từ. Không tải mạng trong lúc render.')
    torch, _, _ = worker.dependencies()
    import numpy as np
    import wave
    device = 'cuda' if args.device != 'cpu' and torch.cuda.is_available() else 'cpu'
    if args.device == 'gpu' and device != 'cuda':
        raise RuntimeError('Bộ căn từ chưa dùng được CUDA; chọn Auto hoặc CPU trong tab phụ đề.')
    with tempfile.TemporaryDirectory(prefix='v15-voice-align-') as directory:
        pcm = Path(directory)/'final.wav'
        cmd = [args.ffmpeg, '-nostdin', '-v', 'error', '-y', '-protocol_whitelist', 'file,pipe', '-i', args.input,
               '-vn', '-ac', '1', '-ar', '16000', '-c:a', 'pcm_s16le', str(pcm)]
        process = subprocess.run(cmd, capture_output=True)
        if process.returncode:
            raise RuntimeError('Không đọc được WAV cuối: '+process.stderr.decode('utf-8', 'replace')[-1000:])
        with wave.open(str(pcm), 'rb') as f:
            duration = f.getnframes()/f.getframerate()
            audio = np.frombuffer(f.readframes(f.getnframes()), dtype=np.int16).astype(np.float32)/32768
        segments, stages = read_units(args.draft, duration)
        try:
            words = worker.align_segments(audio, segments, home, device)
        except Exception as error:
            if args.device != 'auto' or device != 'cuda' or not worker.gpu_error(error):
                raise
            gc.collect()
            torch.cuda.empty_cache()
            device = 'cpu'
            words = worker.align_segments(audio, segments, home, device)
        cues, measured = make_output(words, segments, stages, duration)
        write_result(args.output, cues, measured, duration, device, 'CTC · lời đã biết từ WAV V15.6')
        tree = ET.parse(args.output)
        tree.getroot().set('version', '1.4.11')
        tree.getroot().set('pipeline', 'v1411-anchored-1')
        tree.write(args.output, encoding='utf-8', xml_declaration=True)
        worker.progress(100, 'Đã căn SRT theo WAV · '+str(len(cues))+' khối · '+device)


if __name__ == '__main__':
    main()
