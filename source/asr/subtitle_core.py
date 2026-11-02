"""Subtitle assembly from measured word times; never force a fixed reading rate."""
import math
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from subtitle_quality import terminal,norm


def stamp(seconds):
    n=max(0,round(float(seconds)*1000))
    return f'{n//3600000:02}:{n//60000%60:02}:{n//1000%60:02},{n%1000:03}'


def wrap(text,width=42):
    w=text.split()
    if len(text)<=width or len(w)<2:return text
    choices=[(i,len(' '.join(w[:i])),len(' '.join(w[i:]))) for i in range(1,len(w))]
    i,_,_=min(choices,key=lambda c:max(0,c[1]-width)*10+max(0,c[2]-width)*10+abs(c[1]-c[2]))
    return ' '.join(w[:i])+'\n'+' '.join(w[i:])


def finite(value):
    try:
        value=float(value)
        return value if math.isfinite(value) else None
    except (TypeError,ValueError):return None


def interval(start,end,duration):
    start,end=finite(start),finite(end)
    if start is None or end is None:return None
    start,end=max(0,start),min(duration,end)
    return (start,end) if round(end*1000)>round(start*1000) else None


def timing_units(words,duration):
    """A subtitle needs valid cue times, not a fabricated positive time for every word.

    Unresolved tokens share a neighbouring measured unit, or their measured ASR
    segment window. Their own word times remain explicitly unresolved in metadata.
    """
    units=[];pending=[];last_segment=object()

    def add(unit):
        if units and round(unit['start']*1000)<round(units[-1]['end']*1000):
            previous=units[-1]
            unit['flags'].append('Mốc chồng nhau đã hiệu chỉnh; cần nghe lại')
            if round(unit['end']*1000)>round(previous['end']*1000):
                unit['start']=previous['end']
            else:
                # Contained/reversed word timing: retain text in the preceding
                # measured unit instead of clipping the word to zero duration.
                previous['word']+=' '+unit['word'];previous['flags']+=unit['flags'];previous.setdefault('info',[]).extend(unit.get('info',[]))
                previous['flags'].append('Các từ dùng chung mốc khối; cần nghe lại')
                return
        units.append(unit)

    def flush_pending(next_unit=None):
        if not pending:return next_unit
        text=' '.join(w['word'] for w in pending)
        flags=[f for w in pending for f in w['flags']];info=[f for w in pending for f in w.get('info',[])]
        flags.append('Chưa có mốc riêng cho một số từ; dùng mốc khối, cần nghe lại')
        bounds=[interval(w.get('segment_start'),w.get('segment_end'),duration) for w in pending]
        bounds=[b for b in bounds if b]
        if next_unit is not None:
            next_unit['word']=text+' '+next_unit['word'];next_unit['flags']+=flags;next_unit.setdefault('info',[]).extend(info)
            if bounds:next_unit['start']=min(next_unit['start'],min(b[0] for b in bounds))
        elif units:
            units[-1]['word']+=' '+text;units[-1]['flags']+=flags;units[-1].setdefault('info',[]).extend(info)
            if bounds:units[-1]['end']=max(units[-1]['end'],max(b[1] for b in bounds))
        elif bounds:
            add(dict(word=text,start=min(b[0] for b in bounds),end=max(b[1] for b in bounds),flags=flags,info=info))
        else:raise ValueError('Không có mốc từ hoặc mốc khối hợp lệ để xuất phụ đề. Cần kiểm tra âm thanh.')
        pending.clear();return next_unit

    for source in words:
        text=' '.join(str(source.get('word','')).split())
        if not text:continue
        segment=source.get('segment_id')
        if segment!=last_segment:flush_pending();last_segment=segment
        flags=list(source.get('flags',[]));start,end=source.get('start'),source.get('end')
        measured=interval(start,end,duration)
        previous=units[-1]['end'] if units else 0
        fallback=interval(source.get('asr_start'),source.get('asr_end'),duration)
        if (not measured or round(measured[1]*1000)<=round(previous*1000)) and fallback and round(fallback[1]*1000)>round(previous*1000):
            measured=fallback;flags.append('Căn từ lỗi biên; dùng mốc ASR dự phòng, cần nghe lại')
        if measured:
            if finite(start) is None or finite(end) is None or measured!=(finite(start),finite(end)):
                flags.append('Mốc vượt giới hạn hoặc dự phòng; cần nghe lại')
            unit=dict(word=text,start=measured[0],end=measured[1],flags=flags,info=list(source.get('info',[])))
            source.update(word=text,start=measured[0],end=measured[1],flags=flags,info=list(source.get('info',[])))
            add(flush_pending(unit))
        else:
            flags.append('Từ chưa căn được mốc riêng')
            source.update(word=text,start=None,end=None,flags=flags)
            pending.append(dict(source))
    flush_pending()
    if not units:raise ValueError('Không nhận được lời nói. Kiểm tra âm thanh hoặc luồng tiếng.')
    return units


WEAK_END=set("a an the to of in on at for with and or but if because as my his her their our your its i he she it they we you is was were are has have had will would could should this that these those".split())


def split_group(group,width,maxchars,maxseconds):
    # Dynamic programming chooses boundaries using both neighbouring phrases.
    n=len(group);cost=[float('inf')]*(n+1);back=[None]*(n+1);cost[0]=0
    for i in range(n):
        if back[i] is None and i:continue
        text=''
        for j in range(i+1,n+1):
            text+=(' ' if text else '')+group[j-1]['word'];elapsed=group[j-1]['end']-group[i]['start']
            lines=wrap(text,width).splitlines();fits=len(text)<=maxchars and elapsed<=maxseconds and all(len(l)<=width for l in lines)
            if not fits and j>i+1:break
            # A measured shared-time unit or an unbreakable word must survive.
            penalty=4.0+max(0,1.05-elapsed)*30+max(0,len(text)/max(.05,elapsed)-25)*1.5
            if len(text)/max(.05,elapsed)>25:penalty+=5
            if not fits:penalty+=100
            last=group[j-1]['word'];lastnorm=norm(last)
            if j<n:
                if terminal(last):penalty-=3.5
                elif last.endswith((',', ';', ':')):penalty-=1.8
                if lastnorm in WEAK_END:penalty+=6
                gap=group[j]['start']-group[j-1]['end'];penalty-=min(2,max(0,gap)*4)
            # Prefer keeping a sentence together, but merge tiny sentence tails.
            penalty+=sum(2.5 for w in group[i:j-1] if terminal(w['word']))
            candidate=cost[i]+penalty
            if candidate<cost[j]:cost[j]=candidate;back[j]=i
    pieces=[];j=n
    while j:
        i=back[j]
        if i is None:raise ValueError('Không chia được phụ đề theo mốc đã đo.')
        pieces.append(group[i:j]);j=i
    return list(reversed(pieces))


def assemble(words,duration,compact=False):
    if not math.isfinite(duration) or duration<=0:raise ValueError('Âm thanh không có thời lượng hợp lệ.')
    clean=timing_units(words,duration);width,maxchars,maxseconds=(24,46,3.5) if compact else (42,84,6)
    groups=[];current=[]
    for w in clean:
        if current and w['start']-current[-1]['end']>=.8:groups.append(current);current=[]
        current.append(w)
    if current:groups.append(current)
    cues=[]
    for group in groups:
        for piece in split_group(group,width,maxchars,maxseconds):
            text=' '.join(w['word'] for w in piece);start,end=piece[0]['start'],piece[-1]['end']
            flags=sorted(set(f for w in piece for f in w.get('flags',[])));info=sorted(set(f for w in piece for f in w.get('info',[])))
            wrapped=wrap(text,width)
            if len(text)/max(.01,end-start)>25:flags.append('Chữ chạy nhanh; cần nghe kiểm tra')
            if end-start<.8:flags.append('Khối dưới 0,8 giây; cần kiểm tra')
            if len(text)>maxchars or end-start>maxseconds or any(len(l)>width for l in wrapped.splitlines()):flags.append('Khối dùng mốc chung dài; cần kiểm tra cách chia')
            cues.append(dict(start=start,end=end,text=wrapped,flags=flags,info=info))
    return cues


def write_result(path,cues,words,duration,device,model,audit=None):
    x=ET.Element('subtitles',version='1.4.8',pipeline='v148-verified-1',duration=str(duration),device=device,model=model)
    for c in cues:ET.SubElement(x,'cue',start=str(c['start']),end=str(c['end']),note='; '.join(c.get('flags',[])),info='; '.join(c.get('info',[]))).text=c['text']
    log=ET.SubElement(x,'audit')
    for entry in audit or []:ET.SubElement(log,'change',**{k:str(v) for k,v in entry.items()})
    xs=ET.SubElement(x,'words')
    for w in words:
        attrs=dict(note='; '.join(w.get('flags',[])))
        if interval(w.get('start'),w.get('end'),duration):attrs.update(start=str(w['start']),end=str(w['end']))
        else:attrs['timing']='unresolved'
        ET.SubElement(xs,'word',**attrs).text=w['word']
    p=Path(path);tmp=p.with_suffix('.partial.xml');ET.ElementTree(x).write(tmp,encoding='utf-8',xml_declaration=True);tmp.replace(p)
