"""Run identical full render/re-render work against exact older/current EXEs.

Actual FFmpeg, PCM files, checkpoints and external processes; synthetic speech
and a 150 ms startup delay in the fixture worker. No RTX speed claim.
"""
from pathlib import Path
import hashlib,json,shutil,statistics,subprocess,sys,tempfile
import run_validation as v

def main():
    if len(sys.argv)<3:raise SystemExit('benchmark411.py EXE_3.1 EXE_4.1')
    proof=json.loads((v.VALID/'completed411.json').read_text())
    paths=[('3.1.0',Path(sys.argv[1]).resolve()),('4.1.0',Path(sys.argv[2]).resolve()),('4.1.1',v.ROOT/'V15VoiceStudio.exe')]
    original=v.ROOT;oldenv=dict(v.ENV);result={};work=Path(tempfile.mkdtemp(prefix='beo-render-bench-411-'))
    try:
        for label,exe in paths:
            folder=work/label;folder.mkdir();shutil.copyfile(exe,folder/'V15VoiceStudio.exe');v.ROOT=folder;v.ENV=dict(oldenv,MONO_PATH=str(v.TOOLS/'usr/lib/mono/4.5')+':'+str(folder))
            v.compile('RenderPipelineBenchmark',['RenderPipelineFixture.cs','RenderPipelineBenchmark.cs'],main='RenderPipelineBenchmark',app=True)
            entry={'binary_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'runs':[]}
            for run in range(3):
                r=subprocess.run([v.MONO,str(folder/'RenderPipelineBenchmark.exe')],env=v.ENV,capture_output=True,text=True,timeout=180,check=True)
                lines=[s.split('|') for s in r.stdout.splitlines() if s.startswith('PIPELINE|')];assert len(lines)==4,r.stdout+r.stderr
                for x in lines:
                    assert x[1]==label+'.0'
                    entry['runs'].append(dict(run=run,mode=int(x[2]),warm=bool(int(x[3])),ms=float(x[4]),worker_starts=int(x[5]),tts_requests=int(x[6]),ffmpeg_processes=int(x[7]),wav_sha256=x[8],reports=int(x[9])))
            entry['medians']={str(mode)+'_'+phase:statistics.median(x['ms'] for x in entry['runs'] if x['mode']==mode and x['warm']==(phase=='warm')) for mode in [1,2] for phase in ['cold','warm']}
            result[label]=entry
        assert result['4.1.1']['binary_sha256']==proof['binary_sha256']['V15VoiceStudio.exe']
        hashes={x['wav_sha256'] for e in result.values() for x in e['runs']};assert len(hashes)==1,'The same source/settings must keep identical PCM bytes across versions and cache paths'
        result['scope']='Mono/Linux, actual full workflow and FFmpeg, eight locked cues; synthetic Chatterbox-like Python worker with 150 ms artificial startup delay; 3 runs per version/mode/cache state. Not real TTS/model or RTX/Windows speed.'
        (original/'validation/pipeline411.json').write_text(json.dumps(result,indent=2))
        print(json.dumps({k:{'medians':v['medians'],'binary_sha256':v['binary_sha256']} for k,v in result.items() if k!='scope'},indent=2))
    finally:v.ROOT=original;v.ENV=oldenv;shutil.rmtree(work)

if __name__=='__main__':main()
