"""Conservative, local audio verification. Lexical edits require two agreeing clips.
Punctuation transfer never substitutes words. All automatic edits have an audit trail.
"""
import copy
import difflib
import re

PROTECTED={'had','that','really','very','so','no','yes','bye','ha'}
# 'so so' can be a real intensifier: verify, rather than delete it by rule.
PROTECTED.discard('so')
FALSE_STARTS={("into","to"),("he's","he"),("she's","she"),("it's","it"),("that's","that")}
ABBR={'mr.','mrs.','ms.','dr.','prof.','st.','vs.','u.s.','u.k.','e.g.','i.e.'}


def norm(s):return re.sub(r'[^a-z0-9\']','',s.lower().replace('\u2019',"'"))
def long_unpunctuated(words):return len(words)>=60 or (len(words)>=38 and sum(bool(re.search(r'[,;:]',w['word'])) for w in words)<2)

def terminal(s):return bool(re.search(r'[.!?][\"\u201d\u2019\']?$',s)) and s.lower() not in ABBR


def map_words(text,raw):
    """Map whitespace words to ASR pieces (21 + -year + -old), without losing text."""
    spans=[];chars=''
    for w in raw:
        token=norm(w.get('word',''));spans.append((len(chars),len(chars)+len(token),w));chars+=token
    words=[];position=0
    expected=''.join(norm(t) for t in text.split())
    compatible=expected==chars
    for token in text.split():
        end=position+len(norm(token));parts=[w for a,b,w in spans if b>position and a<end] if compatible else []
        good=[p for p in parts if p.get('start') is not None and p.get('end') is not None]
        flags=list(dict.fromkeys(f for p in parts for f in p.get('flags',[])))
        if not compatible:flags.append('Lời và mốc từ ASR chưa khớp; cần căn lại')
        words.append(dict(word=token,start=min((p['start'] for p in good),default=None),end=max((p['end'] for p in good),default=None),flags=flags,info=[]))
        position=end
    return words


def candidates(words):
    out=[];tokens=[norm(w['word']) for w in words]
    for i in range(1,len(words)):
        if tokens[i] and tokens[i]==tokens[i-1] and tokens[i] not in PROTECTED:out.append((i,i+1))
        elif (tokens[i-1],tokens[i]) in FALSE_STARTS:out.append((i if tokens[i-1]=='into' else i-1,i+1 if tokens[i-1]=='into' else i))
    # Repeated short phrases. Exclude overlap with a single-word candidate.
    for n in (2,3,4):
        for i in range(n,len(words)-n+1):
            if tokens[i-n:i]==tokens[i:i+n] and all(tokens[i:i+n]) and not any(a<i+n and b>i for a,b in out):out.append((i,i+n))
    return sorted(set(out))


def measured_position(words,index):
    for distance in range(len(words)):
        for k in (index-distance,index+distance):
            if 0<=k<len(words) and words[k].get('start') is not None:return words[k]['start']
    return 0


def selected_windows(words,duration):
    targets=[]
    for a,b in candidates(words):targets.append((measured_position(words,a),words[b-1].get('end') or measured_position(words,b-1),'repeat'))
    run=[]
    for i,w in enumerate(words):
        run.append(i)
        if terminal(w['word']) or i==len(words)-1:
            a,b=run[0],run[-1]
            start,end=words[a].get('start'),words[b].get('end')
            if start is not None and end is not None and (long_unpunctuated([words[n] for n in run]) or end-start>=16):
                for k in range(a,b+1,32):
                    s=words[k].get('start')
                    if s is not None:targets.append((s,min(end,s+12),'punctuation'))
            run=[]
    # Also revisit low-confidence recognition, with a strict bounded clip size.
    for w in words:
        if w.get('flags') and w.get('start') is not None:targets.append((w['start'],w.get('end') or w['start'],'weak'))
    windows=[]
    for a,b,kind in sorted(targets):
        if any(a>=start+(0 if start==0 else 2) and b<=end-(0 if end==duration else 2) for start,end in windows):continue
        start=max(0,a-5);end=min(duration,max(a+16,b+3))
        if end-start>28:end=start+28
        if windows and start<=windows[-1][1] and end-windows[-1][0]<=28:windows[-1]=(windows[-1][0],max(end,windows[-1][1]))
        elif not windows or not(start>=windows[-1][0] and end<=windows[-1][1]):windows.append((start,end))
    return windows


def deletion_votes(original,alternative):
    """Only interior deletions with unchanged neighbours are evidence, not rewrites."""
    a=[norm(w['word']) for w in original];b=[norm(w['word']) for w in alternative]
    sm=difflib.SequenceMatcher(None,a,b,autojunk=False);votes=set()
    for tag,i,j,k,l in sm.get_opcodes():
        if tag!='delete' or i<3 or len(a)-j<3 or k<3 or len(b)-l<3:continue
        if a[i-3:i]==b[k-3:k] and a[j:j+3]==b[l:l+3]:votes.update(range(i,j))
    # SequenceMatcher may attach the deletion to the first identical copy.
    # Canonicalize evidence to the second copy; retain one measured token.
    for lo,hi in candidates(original):
        size=hi-lo
        if lo>=size and a[lo-size:lo]==a[lo:hi] and all(i in votes for i in range(lo-size,lo)):votes.update(range(lo,hi))
    return votes


def transfer_punctuation(original,alternative):
    a=[norm(w['word']) for w in original];b=[norm(w['word']) for w in alternative]
    sm=difflib.SequenceMatcher(None,a,b,autojunk=False)
    if not a or sm.ratio()<.86:return 0
    changed=0
    for block in sm.get_matching_blocks():
        if block.size<5:continue
        for offset in range(block.size):
            i,j=block.a+offset,block.b+offset
            if offset<1 or offset>=block.size-1:continue
            old,new=original[i]['word'],alternative[j]['word'];value=old
            punctuation=re.search(r'([,;:.!?]+)([\"\u201d\u2019\']?)$',new)
            if punctuation:
                # Keep the primary spelling. Do not erase an existing sentence stop.
                if not terminal(old) and (terminal(new) or not re.search(r'[,;:]$',old)):
                    value=re.sub(r'[,;:.!?]+$','',old)+punctuation.group(1)
            base=re.sub(r'^[^A-Za-z]*','',new)
            if base[:1].isupper():value=re.sub(r'[A-Za-z]',lambda m:m.group().upper(),value,count=1)
            if value!=old:
                original[i]['word']=value;original[i].setdefault('info',[]).append('Đã đối chiếu dấu câu / viết hoa với âm thanh');changed+=1
    return changed


def verify_segments(segments,audio_duration,recognize,notify=lambda *a:None,punctuate=None):
    """recognize(start,end) returns measured word dictionaries from that audio clip."""
    segments=copy.deepcopy(segments);words=[]
    for s in segments:
        ws=map_words(s['text'],s.get('words',[]))
        words.extend(ws)
    cursor=0
    for si,s in enumerate(segments):
        for _ in s['text'].split():words[cursor]['segment']=si;cursor+=1
    windows=selected_windows(words,audio_duration);hypotheses=[]
    for n,(start,end) in enumerate(windows):
        notify(n,len(windows),f'Đối chiếu âm thanh {start:.1f}–{end:.1f}s')
        alternative=recognize(start,end)
        ids=[i for i,w in enumerate(words) if w.get('start') is not None and w['start']>=start+.02 and (w.get('end') or w['start'])<=end-.02]
        if start==0:ids=[i for i,w in enumerate(words) if w.get('start') is not None and (w.get('end') or 0)<=end-.02]
        if not ids:continue
        lo,hi=min(ids),max(ids)+1
        # Include primary context only; the clip hypotheses include edge words.
        original=words[lo:hi]
        left=original[0].get('start') or 0;right=original[-1].get('end') or end
        alt=[w for w in alternative if (w.get('end') or 0)>left-.15 and (w.get('start') or 0)<right+.15]
        votes=deletion_votes(original,alt);hypotheses.append((lo,hi,alt,{lo+i for i in votes}))
    removed=set();audit=[]
    for a,b in candidates(words):
        supporting=[h for h in hypotheses if a>=h[0] and b<=h[1] and all(i in h[3] for i in range(a,b))]
        confirmed=False
        if supporting:
            start=max(0,(words[max(0,a-6)].get('start') or 0)-.7);end=min(audio_duration,(words[min(len(words)-1,b+6)].get('end') or audio_duration)+.7)
            if end-start<=18:
                # A second, narrower context must independently agree.
                alt=recognize(start,end);ids=[i for i,w in enumerate(words) if w.get('start') is not None and w['start']>=start and (w.get('end') or 0)<=end]
                if ids:
                    lo,hi=min(ids),max(ids)+1;votes=deletion_votes(words[lo:hi],alt)
                    confirmed=all(i-lo in votes for i in range(a,b))
        if confirmed:
            removed.update(range(a,b));owner=max(0,a-1)
            words[owner].setdefault('info',[]).append('Đã bỏ từ lặp qua hai cửa sổ âm thanh')
            audit.append(dict(kind='duplicate',start=words[a].get('start'),removed=' '.join(w['word'] for w in words[a:b])))
        else:
            for w in words[a:b]:w.setdefault('flags',[]).append('Từ / cụm lặp chưa được hai lượt xác nhận; cần nghe lại')
    for lo,hi,alt,votes in hypotheses:
        transfer_punctuation([words[i] for i in range(lo,hi) if i not in removed],alt)
    kept=[w for i,w in enumerate(words) if i not in removed]
    # Local recognition can lose punctuation in rapid narration. A continuous,
    # context-aware pass may supply punctuation only, never replacement wording.
    run=[];needs_punctuation=False
    for w in kept:
        run.append(w)
        if terminal(w['word']):needs_punctuation|=long_unpunctuated(run);run=[]
    needs_punctuation|=long_unpunctuated(run)
    if needs_punctuation and punctuate is not None:
        notify(len(windows),len(windows),'Phục hồi dấu câu với ngữ cảnh liên tục…')
        alternative=punctuate()
        transfer_punctuation(kept,alternative)
    # Persist unresolved long punctuation runs as actionable review flags.
    run=[]
    for i,w in enumerate(kept):
        run.append(w)
        if terminal(w['word']) or i==len(kept)-1:
            if long_unpunctuated(run):
                for x in run:x.setdefault('flags',[]).append('Đoạn dài chưa rõ dấu kết câu; cần nghe lại')
            run=[]
    for si,s in enumerate(segments):
        s['words']=[{k:v for k,v in w.items() if k!='segment'} for w in kept if w['segment']==si]
        s['text']=' '.join(w['word'] for w in s['words'])
    return [s for s in segments if s['text']],audit
