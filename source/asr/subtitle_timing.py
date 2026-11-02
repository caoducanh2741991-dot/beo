"""Conservative word-tail checks using the actual decoded PCM and ASR anchors.

Silence may shorten a stretched CTC tail only when an independent ASR endpoint
agrees. Never extend words, fill gaps, or trim speech solely to meet a CPS limit.
"""
import math

def number(value):
    try:
        value=float(value)
        return value if math.isfinite(value) else None
    except (ValueError,TypeError):return None

def refine_word_ends(words,audio,sample_rate=16000):
    import numpy as np
    result=[];frame=max(1,int(sample_rate*.01));duration=len(audio)/sample_rate
    for source in words:
        w=dict(source,flags=list(source.get('flags',[])),info=list(source.get('info',[])))
        result.append(w)
        start,end,anchor=[number(w.get(k)) for k in ('start','end','asr_end')]
        if None in (start,end,anchor) or not 0<=start<anchor<end<=duration or end-anchor<.25:continue
        lo=max(start,anchor-1.5);pcm=np.asarray(audio[int(lo*sample_rate):int(end*sample_rate)],dtype=np.float32)
        count=len(pcm)//frame
        if count<24:continue
        rms=np.sqrt(np.mean(pcm[:count*frame].reshape(count,frame)**2,axis=1));peak=float(np.max(rms))
        if not math.isfinite(peak) or peak<.005:continue
        threshold=max(.0005,peak*.05)
        voiced=np.flatnonzero(rms>threshold)
        if len(voiced)<3:continue
        last=int(voiced[-1]);silence=(count-last-1)*frame/sample_rate
        measured=lo+(last+1)*frame/sample_rate
        if silence<.18 or abs(measured-anchor)>.16:continue
        revised=min(end,measured+.02)
        if revised<=start+.02 or end-revised<.15:continue
        w['ctc_end_before']=end;w['end']=revised
        w['info'].append('Đã rút đuôi theo khoảng lặng PCM và mốc ASR cùng xác nhận: '+f'{end-revised:.2f}s')
    return result

def review_cue_ends(cues,words):
    """Flag disagreement at exported cue edges, not every internal word."""
    indexed=[w for w in words for _ in str(w.get('word','')).split()];cursor=0
    for cue in cues:
        cursor+=len(cue['text'].split())
        if not 0<cursor<=len(indexed):continue
        w=indexed[cursor-1];anchor=number(w.get('asr_end'));end=number(w.get('end'))
        if anchor is None or end is None or abs(end-anchor)<=.35:continue
        cue['flags']=list(dict.fromkeys(cue.get('flags',[])+['Mốc cuối chưa thống nhất giữa hai bộ căn; cần nghe lại']))
        cue.setdefault('info',[]).append(f'Chênh lệch mốc cuối từ {w["word"]}: {end-anchor:+.2f}s; giữ mốc đo, không tự kéo/cắt lời')
    return cues
