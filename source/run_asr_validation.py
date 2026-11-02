"""Real offline ASR QA. Requires both model packs and an explicitly supplied reference WAV/SRT.
No test media is included in the application update ZIP.
"""
from pathlib import Path
import argparse,json,os,re,subprocess,sys,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent.parent


def main():
    p=argparse.ArgumentParser();p.add_argument('--python',required=True);p.add_argument('--home',required=True);p.add_argument('--audio',required=True);p.add_argument('--srt',required=True);args=p.parse_args()
    folder=ROOT/'validation/real-asr';folder.mkdir(parents=True,exist_ok=True);worker=ROOT/'source/asr/asr_worker.py';home=Path(args.home).resolve()
    expected=' '.join(s for s in Path(args.srt).read_text(encoding='utf-8-sig').splitlines() if s.strip() and not s.strip().isdigit() and '-->' not in s)
    (folder/'reference.txt').write_text(expected,encoding='utf-8')
    env=dict(os.environ,OMP_NUM_THREADS='4')
    def run(name,command='run',media=None,model='large-v3',extra=None,offline=False):
        output=folder/(name+'.xml');a=[str(worker),command,'--home',str(home),'--input',str(media or Path(args.audio).resolve()),'--output',str(output),'--device','cpu','--model',model,'--cache',str(home/'cache')]+(extra or [])
        if offline:
            boot="import sys,runpy;sys.path.insert(0,__import__('os').path.dirname(sys.argv[1]));sys.addaudithook(lambda e,a: (_ for _ in ()).throw(RuntimeError('network forbidden')) if e in ('socket.connect','socket.getaddrinfo') else None);sys.argv=sys.argv[1:];runpy.run_path(sys.argv[0],run_name='__main__')"
            cmd=[args.python,'-c',boot]+a
        else:cmd=[args.python]+a
        with (folder/(name+'.log')).open('w') as log:r=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,env=env,timeout=600)
        if r.returncode:raise RuntimeError(name+' failed; see '+str(folder/(name+'.log')))
        x=ET.parse(output).getroot();previous=0
        for c in x.findall('cue'):
            start,end=float(c.get('start')),float(c.get('end'));assert start>=previous-.001 and end>start and end<=float(x.get('duration'))+.001;previous=end
        print('PASS',name,len(x.findall('cue')),'cues',flush=True);return x
    result=run('offline-full',offline=True)
    normalize=lambda s:re.findall(r"[a-z]+(?:'[a-z]+)?",s.lower())
    a,b=normalize(expected),normalize(' '.join(c.text or '' for c in result.findall('cue')));previous=list(range(len(b)+1))
    for i,w in enumerate(a,1):
        row=[i]
        for j,v in enumerate(b,1):row.append(min(previous[j]+1,row[-1]+1,previous[j-1]+(w!=v)))
        previous=row
    summary={'reference_words':len(a),'recognized_words':len(b),'word_edits':previous[-1],'wer':previous[-1]/len(a),'offline_socket_audit':True,'environment':'CPU/Linux; no physical Windows/GPU claim'}
    mp3=folder/'short.mp3';subprocess.run(['ffmpeg','-v','error','-y','-i',args.audio,'-t','18','-c:a','libmp3lame',str(mp3)],check=True)
    run('mp3',media=mp3);run('realigned',command='align',media=mp3,extra=['--draft',str(folder/'mp3.xml')]);run('reference',extra=['--reference',str(folder/'reference.txt')]);run('distil',media=mp3,model='distil-large-v3')
    delayed=folder/'delayed.mp4';subprocess.run(['ffmpeg','-v','error','-y','-f','lavfi','-i','color=c=black:size=160x90:rate=10:duration=21','-itsoffset','2.5','-i',str(mp3),'-map','0:v:0','-map','1:a:0','-c:v','libx264','-preset','ultrafast','-c:a','aac',str(delayed)],check=True)
    x=run('delayed',media=delayed);summary['delayed_first_cue']=float(x.find('cue').get('start'));assert summary['delayed_first_cue']>=2.4
    for name in ['offline-full','mp3','realigned','reference','distil','delayed']:
        x=ET.parse(folder/(name+'.xml')).getroot();summary[name]={'cues':len(x.findall('cue')),'words':len(x.findall('words/word')),'duration':float(x.get('duration')),'review_cues':sum(bool(c.get('note')) for c in x.findall('cue'))}
    (ROOT/'validation/asr-quality-summary.json').write_text(json.dumps(summary,indent=2));assert summary['wer']==0,'Reference text mismatch; inspect quality report'
    print('REAL ASR QA COMPLETE',json.dumps(summary),flush=True)


if __name__=='__main__':main()
