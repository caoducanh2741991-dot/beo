"""Subtitle assembly from measured word times; never force a fixed reading rate."""
import math
import re
import xml.etree.ElementTree as ET
from pathlib import Path


def stamp(seconds):
    n=max(0,round(float(seconds)*1000))
    return f'{n//3600000:02}:{n//60000%60:02}:{n//1000%60:02},{n%1000:03}'


def wrap(text,width=42):
    w=text.split()
    if len(text)<=width or len(w)<2:return text
    choices=[(i,len(' '.join(w[:i])),len(' '.join(w[i:]))) for i in range(1,len(w))]
    i,_,_=min(choices,key=lambda c:max(0,c[1]-width)*10+max(0,c[2]-width)*10+abs(c[1]-c[2]))
    return ' '.join(w[:i])+'\n'+' '.join(w[i:])


def assemble(words,duration,compact=False):
    if not math.isfinite(duration) or duration<=0:raise ValueError('Âm thanh không có thời lượng hợp lệ.')
    clean=[];previous=0
    for source in words:
        w=dict(source);w['word']=' '.join(str(w.get('word','')).split())
        if not w['word']:continue
        flags=list(w.get('flags',[]));start,end=float(w.get('start',float('nan'))),float(w.get('end',float('nan')))
        if not math.isfinite(start) or not math.isfinite(end):raise ValueError('Có từ chưa có mốc. Cần căn lại đoạn đó.')
        if round(min(duration,end)*1000)<=round(max(previous,start,0)*1000):
            fs,fe=w.get('asr_start'),w.get('asr_end')
            if fs is not None and fe is not None and round(min(duration,fe)*1000)>round(max(previous,fs,0)*1000):
                start,end=float(fs),float(fe);flags.append('Căn từ lỗi biên; dùng mốc ASR dự phòng, cần nghe lại')
        if start<previous-.001:flags.append('Mốc chồng nhau đã hiệu chỉnh; cần nghe lại');start=previous
        if start<0 or end>duration:flags.append('Mốc vượt giới hạn đã hiệu chỉnh')
        start,end=max(0,start),min(duration,end)
        if round(end*1000)<=round(start*1000):raise ValueError('Có từ không đủ thời lượng hoặc vượt âm thanh. Cần căn lại.')
        w.update(start=start,end=end,flags=flags);source.update(start=start,end=end,flags=flags);clean.append(w);previous=end
    if not clean:raise ValueError('Không nhận được lời nói. Kiểm tra âm thanh hoặc luồng tiếng.')
    maxchars,maxseconds=(46,3.5) if compact else (84,6)
    cues=[];current=[]
    def flush():
        if not current:return
        text=' '.join(w['word'] for w in current);flags=sorted(set(f for w in current for f in w.get('flags',[])))
        start,end=current[0]['start'],current[-1]['end']
        if len(text)/max(.01,end-start)>25:flags.append('Chữ chạy nhanh; cần nghe kiểm tra')
        cues.append(dict(start=start,end=end,text=wrap(text,24 if compact else 42),flags=flags));current.clear()
    abbr={'mr.','mrs.','ms.','dr.','prof.','st.','vs.','u.s.','u.k.','e.g.','i.e.'}
    for w in clean:
        if current:
            pause=w['start']-current[-1]['end'];pausebreak=pause>=1 or (pause>=.55 and (len(current)>=3 or current[-1]['end']-current[0]['start']>=.8))
            if pausebreak or sum(len(t['word'])+1 for t in current)+len(w['word'])>maxchars or w['end']-current[0]['start']>maxseconds:flush()
        current.append(w)
        if re.search(r'[.!?]["\u201d\u2019\']?$',w['word']) and w['word'].lower() not in abbr:flush()
        elif compact and len(current)>=5 and re.search(r'[,;:]$',w['word']):flush()
    flush();return cues


def write_result(path,cues,words,duration,device,model):
    x=ET.Element('subtitles',version='1.4.7',duration=str(duration),device=device,model=model)
    for c in cues:ET.SubElement(x,'cue',start=str(c['start']),end=str(c['end']),note='; '.join(c.get('flags',[]))).text=c['text']
    xs=ET.SubElement(x,'words')
    for w in words:ET.SubElement(xs,'word',start=str(w['start']),end=str(w['end']),note='; '.join(w.get('flags',[]))).text=w['word']
    p=Path(path);tmp=p.with_suffix('.partial.xml');ET.ElementTree(x).write(tmp,encoding='utf-8',xml_declaration=True);tmp.replace(p)
