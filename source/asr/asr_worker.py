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


def progress(n,text):
    print('V15_ASR\t'+str(int(n))+'\t'+base64.b64encode(text.encode('utf-8')).decode(),flush=True)


def decode(ffmpeg,media,target,track=0):
    cmd=[ffmpeg,'-nostdin','-v','error','-y','-copyts','-start_at_zero','-i',str(media),'-map',f'0:a:{track}',
         '-vn','-t','7201','-ac','1','-af','aresample=16000:async=1:first_pts=0','-ar','16000','-c:a','pcm_s16le',str(target)]
    r=subprocess.run(cmd,capture_output=True,creationflags=0x08000000 if os.name=='nt' else 0)
    if r.returncode:raise RuntimeError('Không đọc được luồng âm thanh. Kiểm tra file và số luồng tiếng. '+r.stderr.decode('utf-8','replace')[-2000:])


def dependencies():
    os.environ.update(HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',HF_HUB_DISABLE_TELEMETRY='1')
    import importlib.util
    spec=importlib.util.find_spec('torch')
    if spec and spec.submodule_search_locations:
        lib=str(Path(next(iter(spec.submodule_search_locations)))/'lib');os.environ['PATH']=lib+os.pathsep+os.environ.get('PATH','')
        if os.name=='nt':
            global dll_handle
            dll_handle=os.add_dll_directory(lib)
    import torch,torchaudio,ctranslate2
    import whisperx.alignment
    torch.set_num_threads(max(1,min(8,(os.cpu_count() or 4)-1)))
    return torch,torchaudio,ctranslate2


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
        if not words:raise RuntimeError('Đoạn nhận dạng không có mốc từ. Cần chạy lại file.')
        if s.avg_logprob < -1 or s.no_speech_prob>.6:
            for w in words:w['flags'].append('Đoạn nhận dạng yếu; cần nghe lại')
        segments.append(dict(start=s.start,end=s.end,text=s.text.strip(),words=words))
        progress(15+40*min(1,s.end/max(1,info.duration)),f'Nhận dạng {s.end:.1f}s / {info.duration:.1f}s')
    del model;gc.collect();return segments


def align_segments(audio,segments,home,device):
    import torch,torchaudio
    import whisperx.alignment as wx
    from faster_whisper.vad import get_speech_timestamps,VadOptions
    if not segments:raise RuntimeError('Không nhận được lời nói trong file.')
    progress(58,'Nạp mô hình căn thời gian từng từ · '+device)
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
        good=len(aligned)==len(expected) and all(a.get('word','').strip()==b for a,b in zip(aligned,expected))
        for i,text in enumerate(expected):
            a=aligned[i] if good else {};flags=list(fallback[i].get('flags',[])) if i<len(fallback) else [];flags+=s.get('flags',[])
            if a.get('start') is not None and a.get('end') is not None and a['end']>a['start']:
                if float(a.get('score',0))<.45:flags.append('Căn từ chưa chắc chắn')
                if any(c.isdigit() for c in text):flags.append('Số liệu; cần kiểm tra cách đọc')
                w=dict(word=text,start=a['start']+start,end=a['end']+start,flags=flags)
                if i<len(fallback) and fallback[i]['word'].strip()==text:w.update(asr_start=fallback[i]['start'],asr_end=fallback[i]['end'])
                words.append(w)
            elif i<len(fallback) and fallback[i]['word'].strip()==text:
                w=dict(fallback[i]);w['flags']=flags+['Dùng mốc ASR dự phòng; cần nghe lại'];words.append(w)
            else:raise RuntimeError('Không căn được từ "'+text+'". Kiểm tra lời sửa có khớp âm thanh không.')
        progress(60+35*(index+1)/len(segments),f'Căn timeline {index+1}/{len(segments)} đoạn')
    del model;gc.collect()
    if device=='cuda':torch.cuda.empty_cache()
    return words


def merge_alignment_windows(segments):
    blocks=[]
    for s in segments:
        if blocks and s['end']-blocks[-1]['start']<=25 and s['start']-blocks[-1]['end']<1.5:
            blocks[-1]['end']=s['end'];blocks[-1]['text']+=' '+s['text']
        else:blocks.append(dict(start=s['start'],end=s['end'],text=s['text']))
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
        block['text']=' '.join(supplied[begin:end]);block['flags']=['Căn theo TXT; cần đối chiếu lời gốc'];index+=n
    return blocks


def fingerprint(media,model,track):
    h=hashlib.sha256()
    with open(media,'rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    h.update(f'v147:{model}:en:track{track}'.encode());return h.hexdigest()


def main():
    p=argparse.ArgumentParser();p.add_argument('command',choices=['run','align','probe','preview']);p.add_argument('--home',required=True)
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
    if args.command=='probe':progress(100,'Đã kiểm tra thư viện và dữ liệu · '+(torch.cuda.get_device_name(0) if device=='cuda' else 'CPU')+' · '+args.model);return
    progress(2,'Kiểm tra âm thanh và bộ nhớ đệm…')
    with tempfile.TemporaryDirectory(prefix='v15-subtitle-') as tmp:
        wav=Path(tmp)/'source.wav';decode(args.ffmpeg,args.input,wav,args.track)
        import numpy as np
        with wave.open(str(wav)) as f:
            duration=f.getnframes()/16000
            if duration>7200:raise ValueError('Mỗi file phụ đề tối đa 2 giờ; hãy chia file dài hơn trước.')
            audio=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype('float32')/32768
        if args.command=='align':
            doc=ET.parse(args.draft).getroot();segments=[dict(start=float(c.get('start')),end=float(c.get('end')),text=' '.join((c.text or '').split())) for c in doc.findall('cue')]
            if any(s['start']<0 or s['end']<=s['start'] or s['end']>duration+.05 or not s['text'] for s in segments):raise ValueError('Mốc hoặc lời sửa không hợp lệ.')
            segments=merge_alignment_windows(segments)
        else:
            cache=Path(args.cache)/(fingerprint(args.input,args.model,args.track)+'.json') if args.cache else None
            if cache and cache.is_file():segments=json.loads(cache.read_text(encoding='utf-8'));progress(55,'Dùng lại bản nhận dạng đã lưu; đang căn timeline…')
            else:
                try:segments=transcribe(audio,path,device)
                except Exception as e:
                    if args.device!='auto' or device!='cuda' or not gpu_error(e):raise
                    gc.collect();torch.cuda.empty_cache();device='cpu';progress(12,'CUDA gặp lỗi; nhận dạng lại bằng CPU…');segments=transcribe(audio,path,device)
                if cache:
                    cache.parent.mkdir(parents=True,exist_ok=True);t=cache.with_suffix('.partial.json');t.write_text(json.dumps(segments,ensure_ascii=False),encoding='utf-8');t.replace(cache)
            if args.reference:segments=use_reference(segments,Path(args.reference).read_text(encoding='utf-8-sig'))
        if cuda:torch.cuda.empty_cache()
        try:words=align_segments(audio,segments,home,device)
        except Exception as e:
            if args.device!='auto' or device!='cuda' or not gpu_error(e):raise
            gc.collect();torch.cuda.empty_cache();device='cpu';progress(58,'CUDA căn mốc gặp lỗi; căn lại bằng CPU…');words=align_segments(audio,segments,home,device)
        cues=assemble(words,duration,args.compact);write_result(args.output,cues,words,duration,device,args.model)
        progress(100,f'Đã nhận {len(cues)} khối · {sum(bool(c["flags"]) for c in cues)} đoạn cần kiểm tra · {device}')


if __name__=='__main__':
    try:main()
    except Exception as e:progress(0,'Lỗi phụ đề: '+str(e));traceback.print_exc(file=sys.stderr);sys.exit(1)
