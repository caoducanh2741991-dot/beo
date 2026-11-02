"""Strictly local speech recognition and word alignment. Downloads live in setup_asr.py."""
import argparse
import base64
import difflib
import gc
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import traceback
import wave
import xml.etree.ElementTree as ET
from pathlib import Path
from subtitle_core import assemble,write_result
from subtitle_quality import map_words,verify_segments


def progress(n,text):
    print('V15_ASR\t'+str(int(n))+'\t'+base64.b64encode(text.encode('utf-8')).decode(),flush=True)


def decode(ffmpeg,media,target,track=0):
    cmd=[ffmpeg,'-nostdin','-v','error','-y','-protocol_whitelist','file,pipe','-copyts','-start_at_zero','-i',str(media),'-map',f'0:a:{track}',
         '-vn','-t','7201','-ac','1','-af','aresample=16000:async=1:first_pts=0','-ar','16000','-c:a','pcm_s16le',str(target)]
    r=subprocess.run(cmd,capture_output=True,creationflags=0x08000000 if os.name=='nt' else 0)
    if r.returncode:raise RuntimeError('Không đọc được luồng âm thanh. Kiểm tra file và số luồng tiếng. '+r.stderr.decode('utf-8','replace')[-2000:])


def dependencies():
    os.environ.update(HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',HF_HUB_DISABLE_TELEMETRY='1')
    import importlib.util
    ensure_packaging()
    spec=importlib.util.find_spec('torch')
    if spec and spec.submodule_search_locations:
        lib=str(Path(next(iter(spec.submodule_search_locations)))/'lib');os.environ['PATH']=lib+os.pathsep+os.environ.get('PATH','')
        if os.name=='nt':
            global dll_handle
            dll_handle=os.add_dll_directory(lib)
    import onnxruntime
    onnxruntime.disable_telemetry_events()
    sys.addaudithook(lambda event,args: (_ for _ in ()).throw(PermissionError('Bộ phụ đề chạy offline; kết nối mạng bị chặn.')) if event in ('socket.connect','socket.getaddrinfo') else None)
    import torch,torchaudio,ctranslate2
    import whisperx.alignment
    torch.set_num_threads(max(1,min(8,(os.cpu_count() or 4)-1)))
    return torch,torchaudio,ctranslate2


def ensure_packaging():
    """Repair the Windows-only CTranslate2 import dependency from a bundled wheel."""
    import importlib.util
    if importlib.util.find_spec('pkg_resources') is not None:return
    wheel=Path(__file__).with_name('setuptools-75.8.2-py3-none-any.whl')
    if not wheel.is_file():raise RuntimeError('Thiếu pkg_resources. Cài ZIP sửa lỗi phụ đề 1.4.7 trước khi chạy lại.')
    if hashlib.sha256(wheel.read_bytes()).hexdigest()!='558e47c15f1811c1fa7adbd0096669bf76c1d3f433f58324df69f3f5ecac4e8f':raise RuntimeError('Gói sửa phụ đề bị thiếu hoặc hỏng. Tải lại ZIP sửa lỗi.')
    progress(1,'Đang bổ sung thư viện phụ đề từ gói sửa offline…')
    r=subprocess.run([sys.executable,'-m','pip','install','--disable-pip-version-check','--no-index','--no-deps','--force-reinstall',str(wheel)],capture_output=True,creationflags=0x08000000 if os.name=='nt' else 0)
    if r.returncode:raise RuntimeError('Không bổ sung được pkg_resources. '+r.stderr.decode('utf-8','replace')[-2000:])
    importlib.invalidate_caches()
    if importlib.util.find_spec('pkg_resources') is None:raise RuntimeError('Chưa nạp được pkg_resources sau khi sửa. Đóng app và mở lại.')


def check_data(home,model):
    path=home/'models'/model
    for name in ('model.bin','config.json','tokenizer.json'):
        if not (path/name).is_file():raise RuntimeError('Chưa có bộ nhận dạng '+model+'. Bấm Cài bộ phụ đề khi có mạng trước.')
    if not (home/'models/alignment/wav2vec2_fairseq_base_ls960_asr_ls960.pth').is_file():raise RuntimeError('Thiếu mô hình căn thời gian. Bấm Cài bộ phụ đề để bổ sung.')
    return path


def gpu_error(e):return any(s in str(e).lower() for s in ('cuda','cudnn','cublas','out of memory','driver','gpu','dll'))


def transcribe(audio,path,device):
    from faster_whisper import WhisperModel
    progress(12,'Nạp mô hình nhận dạng · '+device)
    model=WhisperModel(str(path),device=device,compute_type='int8_float16' if device=='cuda' else 'int8',cpu_threads=max(1,min(8,(os.cpu_count() or 4)-1)),num_workers=1,local_files_only=True)
    result,info=model.transcribe(audio,language='en',task='transcribe',beam_size=5,word_timestamps=True,vad_filter=True,
        vad_parameters=dict(min_silence_duration_ms=500,speech_pad_ms=250),condition_on_previous_text=False,temperature=0,hallucination_silence_threshold=2)
    segments=[]
    for s in result:
        if not s.text.strip():continue
        words=[dict(word=w.word.strip(),start=w.start,end=w.end,flags=[] if w.probability>=.55 else ['Nhận dạng chưa chắc chắn']) for w in s.words or []]
        if s.avg_logprob < -1 or s.no_speech_prob>.6:
            for w in words:w['flags'].append('Đoạn nhận dạng yếu; cần nghe lại')
        segments.append(dict(start=s.start,end=s.end,text=s.text.strip(),words=words))
        progress(15+38*min(1,s.end/max(1,info.duration)),f'Nhận dạng {s.end:.1f}s / {info.duration:.1f}s')
    segments=verify_loaded(audio,segments,model)
    del model;gc.collect();return segments


def verify_loaded(audio,segments,model):
    def recognize(start,end,continuous=False):
        clip=audio[max(0,int(start*16000)):min(len(audio),int(end*16000))]
        result,_=model.transcribe(clip,language='en',task='transcribe',beam_size=5,word_timestamps=True,vad_filter=False,condition_on_previous_text=continuous,temperature=0)
        words=[]
        for segment in result:
            for w in segment.words or []:words.append(dict(word=w.word.strip(),start=w.start+start,end=w.end+start,flags=[]))
        if os.environ.get('V15_ASR_QA_TRACE'):
            with open(os.environ['V15_ASR_QA_TRACE'],'a',encoding='utf-8') as log:log.write(json.dumps(dict(start=start,end=end,continuous=continuous,words=words),ensure_ascii=False)+'\n')
        return words
    def punctuate():
        result,_=model.transcribe(audio,language='en',beam_size=5,word_timestamps=True,vad_filter=True,condition_on_previous_text=True,temperature=0)
        words=[]
        for segment in result:
            progress(68,f'Đối chiếu dấu câu {segment.end:.1f}s / {len(audio)/16000:.1f}s')
            words.extend(dict(word=w.word.strip(),start=w.start,end=w.end) for w in segment.words or [])
        return words
    progress(54,'Rà từ lặp, dấu câu và đoạn nhận dạng yếu…')
    segments,audit=verify_segments(segments,len(audio)/16000,recognize,lambda n,total,message:progress(54+int(14*n/max(1,total)),message),punctuate=punctuate,contextual=lambda start,end:recognize(start,end,True))
    if segments:segments[0]['audit']=audit
    return segments


def retry_weak_alignment(aligned,expected,fallback,base_start,audio,align_clip):
    """One bounded local CTC retry around weak words; never invent word durations."""
    if len(aligned)!=len(expected):return aligned,set()
    revised=[dict(w) for w in aligned];repaired=set();done=set()
    for i,a in enumerate(aligned):
        invalid=a.get('start') is None or a.get('end') is None or a.get('end',0)<=a.get('start',0)
        if not invalid and float(a.get('score',0))>=.25:continue
        lo,hi=max(0,i-2),min(len(expected),i+3)
        if (lo,hi) in done:continue
        done.add((lo,hi));raw=fallback[lo:hi]
        if len(raw)!=hi-lo or any(w.get('start') is None or w.get('end') is None for w in raw):continue
        start=max(0,raw[0]['start']-.3);end=min(len(audio)/16000,raw[-1]['end']+.3)
        if not .15<end-start<=10:continue
        result=align_clip(start,end,' '.join(expected[lo:hi]))
        if len(result)!=hi-lo or any(w.get('word','').strip()!=expected[lo+k] for k,w in enumerate(result)):continue
        for k,b in enumerate(result,lo):
            if b.get('start') is None or b.get('end') is None or b['end']<=b['start']:continue
            old=revised[k];oldscore=float(old.get('score',0));newscore=float(b.get('score',0))
            valid=old.get('start') is not None and old.get('end') is not None and old['end']>old['start']
            if valid and (oldscore>=.45 or newscore<oldscore+.05):continue
            begin,finish=b['start']+start-base_start,b['end']+start-base_start
            if valid and abs(begin+base_start-fallback[k]['start'])>max(.7,abs(old['start']+base_start-fallback[k]['start'])):continue
            revised[k]=dict(b,start=begin,end=finish);repaired.add(k)
    return revised,repaired


def align_segments(audio,segments,home,device):
    import torch,torchaudio
    import whisperx.alignment as wx
    from faster_whisper.vad import get_speech_timestamps,VadOptions
    if not segments:raise RuntimeError('Không nhận được lời nói trong file.')
    progress(70,'Nạp mô hình căn thời gian từng từ · '+device)
    bundle=torchaudio.pipelines.WAV2VEC2_ASR_BASE_960H
    model=bundle.get_model(dl_kwargs={'model_dir':str(home/'models/alignment')}).to(device)
    metadata={'language':'en','dictionary':{c.lower():i for i,c in enumerate(bundle.get_labels())},'type':'torchaudio'}
    speech=get_speech_timestamps(audio,VadOptions(min_speech_duration_ms=100,min_silence_duration_ms=300,speech_pad_ms=30))
    words=[]
    for index,s in enumerate(segments):
        start,end=max(0,s['start']-.15),min(len(audio)/16000,s['end']+.15)
        spans=[p for p in speech if p['end']/16000>s['start']+.02 and p['start']/16000<s['end']-.02]
        if spans:
            a,b=max(start,spans[0]['start']/16000),min(end,spans[-1]['end']/16000)
            if b-a>=.1:start,end=a,b
        if end-start>40:raise RuntimeError('Đoạn căn quá dài. Chia văn bản thành các câu ngắn hơn trước khi căn lại.')
        clip=audio[int(start*16000):int(end*16000)]
        r=wx.align([dict(start=0,end=len(clip)/16000,text=s['text'])],model,metadata,clip,device,interpolate_method='nearest',return_char_alignments=False)
        aligned=r.get('word_segments',[]);expected=s['text'].split();fallback=s.get('words',[])
        def align_clip(a,b,text):
            part=audio[int(a*16000):int(b*16000)]
            return wx.align([dict(start=0,end=len(part)/16000,text=text)],model,metadata,part,device,interpolate_method='nearest',return_char_alignments=False).get('word_segments',[])
        aligned,repaired=retry_weak_alignment(aligned,expected,fallback,start,audio,align_clip)
        good=len(aligned)==len(expected) and all(a.get('word','').strip()==b for a,b in zip(aligned,expected))
        for i,text in enumerate(expected):
            a=aligned[i] if good else {};flags=list(fallback[i].get('flags',[])) if i<len(fallback) else [];flags+=s.get('flags',[]);info=list(fallback[i].get('info',[])) if i<len(fallback) else []
            if a.get('start') is not None and a.get('end') is not None and a['end']>a['start']:
                if float(a.get('score',0))<.45:flags.append('Căn từ chưa chắc chắn')
                if any(c.isdigit() for c in text):flags.append('Số liệu; cần kiểm tra cách đọc')
                w=dict(word=text,start=a['start']+start,end=a['end']+start,flags=flags)
                if i<len(fallback) and fallback[i]['word'].strip()==text:w.update(asr_start=fallback[i]['start'],asr_end=fallback[i]['end'])
                words.append(w)
            elif i<len(fallback) and fallback[i]['word'].strip()==text:
                w=dict(fallback[i]);w['flags']=flags+['Dùng mốc ASR dự phòng; cần nghe lại'];words.append(w)
            else:words.append(dict(word=text,start=None,end=None,flags=flags+['Chưa căn được từ; cần đối chiếu lời gốc']))
            if i in repaired:info.append('Đã căn lại đoạn âm thanh ngắn để sửa mốc yếu')
            words[-1]['info']=info
            if i<len(fallback) and fallback[i].get('start') is not None and words[-1].get('start') is not None and abs(words[-1]['start']-fallback[i]['start'])>.7:words[-1]['flags'].append('Mốc căn khác ASR trên 0,7 giây; cần nghe lại')
            words[-1].update(segment_id=index,segment_start=max(0,s['start']),segment_end=min(len(audio)/16000,s['end']))
        progress(72+25*(index+1)/len(segments),f'Căn timeline {index+1}/{len(segments)} đoạn')
    del model;gc.collect()
    if device=='cuda':torch.cuda.empty_cache()
    return words


def merge_alignment_windows(segments):
    blocks=[]
    for s in segments:
        if blocks and s['end']-blocks[-1]['start']<=25 and s['start']-blocks[-1]['end']<1.5:
            block=blocks[-1];block['end']=s['end'];block['text']+=' '+s['text']
            block['words'].extend(s.get('words',[]));block['flags']=sorted(set(block['flags']+s.get('flags',[])));block['audit'].extend(s.get('audit',[]))
        else:blocks.append(dict(start=s['start'],end=s['end'],text=s['text'],words=list(s.get('words',[])),flags=list(s.get('flags',[])),audit=list(s.get('audit',[]))))
    return blocks


def use_reference(segments,text):
    supplied=text.split();recognized=' '.join(s['text'] for s in segments).split()
    norm=lambda t:''.join(c for c in t.lower() if c.isalnum())
    a,b=[norm(w) for w in recognized],[norm(w) for w in supplied];matcher=difflib.SequenceMatcher(None,a,b,autojunk=False)
    if not a or not b or matcher.ratio()<.65 or not .5<=len(b)/len(a)<=1.5:raise ValueError('TXT khác nhiều so với lời nhận dạng. Kiểm tra đúng file hoặc chạy không kèm TXT để sửa từng đoạn.')
    boundaries={}
    for tag,i,j,k,l in matcher.get_opcodes():
        if j==i:boundaries[i]=k;continue
        for n in range(i,j+1):boundaries[n]=k+round((n-i)*(l-k)/(j-i))
    boundaries[0],boundaries[len(a)]=0,len(b);blocks=merge_alignment_windows(segments);index=0
    for block in blocks:
        n=len(block['text'].split());begin,end=boundaries[index],boundaries[index+n]
        if end<=begin:raise ValueError('TXT thiếu cả đoạn lời nói. Kiểm tra nội dung trước khi căn.')
        block['text']=' '.join(supplied[begin:end]);block['words']=[];block['flags']=['Căn theo TXT; cần đối chiếu lời gốc'];index+=n
    return blocks


def read_cache(path):
    if path is None or not path.is_file() or path.stat().st_size>64*1024*1024:return None
    try:
        data=json.loads(path.read_text(encoding='utf-8'))
        if not isinstance(data,dict) or data.get('schema')!='v149-verified-1':return None
        segments=data.get('segments')
        if not isinstance(segments,list) or not segments:return None
        for segment in segments:
            if not isinstance(segment,dict) or not isinstance(segment.get('text'),str) or not segment['text'].strip():return None
            if not isinstance(segment.get('words'),list):return None
            if any(not isinstance(w,dict) or not isinstance(w.get('word'),str) for w in segment['words']):return None
            if not 0<=float(segment['start'])<float(segment['end'])<=7201:return None
        return segments
    except (OSError,ValueError,TypeError,KeyError):return None


def fingerprint(media,model,track):
    h=hashlib.sha256()
    with open(media,'rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    h.update(f'v149-verified-1:{model}:en:track{track}'.encode());return h.hexdigest()


def main():
    p=argparse.ArgumentParser();p.add_argument('command',choices=['run','align','refine','probe','preview']);p.add_argument('--home',required=True)
    for arg in ['input','output','cache','draft','reference']:p.add_argument('--'+arg)
    p.add_argument('--ffmpeg',default='ffmpeg');p.add_argument('--model',default='large-v3');p.add_argument('--device',choices=['auto','gpu','cpu'],default='auto');p.add_argument('--track',type=int,default=0);p.add_argument('--compact',action='store_true');p.add_argument('--start',type=float,default=0);p.add_argument('--end',type=float,default=5)
    args=p.parse_args();home=Path(args.home)
    if args.command=='preview':
        with tempfile.TemporaryDirectory(prefix='v15-listen-') as tmp:
            full=Path(tmp)/'full.wav';decode(args.ffmpeg,args.input,full,args.track)
            with wave.open(str(full),'rb') as f:
                start=max(0,min(f.getnframes(),int(args.start*16000)));count=max(0,min(f.getnframes()-start,int((args.end-args.start)*16000)))
                if count<=0:raise ValueError('Đoạn nghe không có âm thanh.')
                f.setpos(start)
                with wave.open(args.output,'wb') as dst:dst.setparams(f.getparams());dst.writeframes(f.readframes(count))
        return
    torch,torchaudio,ct=dependencies();path=check_data(home,args.model)
    try:cuda=torch.cuda.is_available() and ct.get_cuda_device_count()>0
    except Exception:
        if args.device=='gpu':raise
        cuda=False
    device='cuda' if cuda and args.device!='cpu' else 'cpu'
    if args.device=='gpu' and not cuda:raise RuntimeError('Bộ phụ đề chưa dùng được CUDA. Chọn Auto/CPU hoặc kiểm tra driver NVIDIA.')
    if args.command=='probe':
        from subtitle_phrases import tagger
        tagger()
        progress(100,'Đã kiểm tra thư viện và dữ liệu · '+(torch.cuda.get_device_name(0) if device=='cuda' else 'CPU')+' · '+args.model);return
    progress(2,'Kiểm tra âm thanh và bộ nhớ đệm…')
    with tempfile.TemporaryDirectory(prefix='v15-subtitle-') as tmp:
        wav=Path(tmp)/'source.wav';decode(args.ffmpeg,args.input,wav,args.track)
        import numpy as np
        with wave.open(str(wav)) as f:
            duration=f.getnframes()/16000
            if duration>7200:raise ValueError('Mỗi file phụ đề tối đa 2 giờ; hãy chia file dài hơn trước.')
            audio=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype('float32')/32768
        if args.command in ('align','refine'):
            doc=ET.parse(args.draft).getroot();segments=[dict(start=float(c.get('start')),end=float(c.get('end')),text=' '.join((c.text or '').split())) for c in doc.findall('cue')]
            if any(s['start']<0 or s['end']<=s['start'] or s['end']>duration+.05 or not s['text'] for s in segments):raise ValueError('Mốc hoặc lời sửa không hợp lệ.')
            segments=merge_alignment_windows(segments)
            if args.command=='refine':
                progress(8,'Đo lại mốc lời hiện có trước khi rà lỗi…')
                try:measured=align_segments(audio,segments,home,device)
                except Exception as e:
                    if args.device!='auto' or device!='cuda' or not gpu_error(e):raise
                    gc.collect();torch.cuda.empty_cache();device='cpu';measured=align_segments(audio,segments,home,device)
                for i,segment in enumerate(segments):segment['words']=[dict(word=w['word'],start=w.get('start'),end=w.get('end'),flags=[]) for w in measured if w.get('segment_id')==i]
                oldaudit=[dict(c.attrib) for c in doc.findall('audit/change')]
                from faster_whisper import WhisperModel
                def verify(device):
                    model=WhisperModel(str(path),device=device,compute_type='int8_float16' if device=='cuda' else 'int8',cpu_threads=max(1,min(8,(os.cpu_count() or 4)-1)),local_files_only=True)
                    try:return verify_loaded(audio,segments,model)
                    finally:del model;gc.collect()
                try:segments=verify(device)
                except Exception as e:
                    if args.device!='auto' or device!='cuda' or not gpu_error(e):raise
                    gc.collect();torch.cuda.empty_cache();device='cpu';segments=verify(device)
                if segments:segments[0]['audit']=oldaudit+segments[0].get('audit',[])
        else:
            cache=Path(args.cache)/(fingerprint(args.input,args.model,args.track)+'.json') if args.cache else None
            segments=read_cache(cache)
            if segments is not None:progress(69,'Dùng lại bản nhận dạng đã đối chiếu; đang căn timeline…')
            else:
                try:segments=transcribe(audio,path,device)
                except Exception as e:
                    if args.device!='auto' or device!='cuda' or not gpu_error(e):raise
                    gc.collect();torch.cuda.empty_cache();device='cpu';progress(12,'CUDA gặp lỗi; nhận dạng lại bằng CPU…');segments=transcribe(audio,path,device)
                if cache:
                    try:
                        cache.parent.mkdir(parents=True,exist_ok=True);t=cache.with_suffix('.partial.json');t.write_text(json.dumps(dict(schema='v149-verified-1',segments=segments),ensure_ascii=False),encoding='utf-8');t.replace(cache)
                    except OSError:progress(69,'Không lưu được bộ nhớ đệm; vẫn tiếp tục căn và xuất phụ đề…')
            if args.reference:segments=use_reference(segments,Path(args.reference).read_text(encoding='utf-8-sig'))
        segments=merge_alignment_windows(segments)
        if cuda:torch.cuda.empty_cache()
        try:words=align_segments(audio,segments,home,device)
        except Exception as e:
            if args.device!='auto' or device!='cuda' or not gpu_error(e):raise
            gc.collect();torch.cuda.empty_cache();device='cpu';progress(70,'CUDA căn mốc gặp lỗi; căn lại bằng CPU…');words=align_segments(audio,segments,home,device)
        cues=assemble(words,duration,args.compact);write_result(args.output,cues,words,duration,device,args.model,audit=[entry for segment in segments for entry in segment.get('audit',[])])
        progress(100,f'Đã nhận {len(cues)} khối · {sum(bool(c["flags"]) for c in cues)} đoạn cần kiểm tra · {device}')


if __name__=='__main__':
    try:main()
    except Exception as e:progress(0,'Lỗi phụ đề: '+str(e));traceback.print_exc(file=sys.stderr);sys.exit(1)
