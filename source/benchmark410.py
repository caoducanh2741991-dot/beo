"""Compare the same UI/cache workload against two exact compiled assemblies.

No speech model is invoked. These measurements do not predict RTX/TTS speed.
Run after validate410.py, with the baseline EXE path as the sole argument.
"""
from pathlib import Path
import hashlib,json,shutil,statistics,subprocess,sys,tempfile
import run_validation as v

def main():
    baseline=Path(sys.argv[1]).resolve()
    assemblies=[('baseline',baseline),('updated',v.ROOT/'V15VoiceStudio.exe')]
    original_root,original_valid,original_env=v.ROOT,v.VALID,dict(v.ENV)
    result={}
    work=Path(tempfile.mkdtemp(prefix='beo-benchmark-410-'))
    try:
        for label,assembly in assemblies:
            root=work/label;root.mkdir();(root/'validation').mkdir()
            shutil.copyfile(assembly,root/'V15VoiceStudio.exe')
            (root/'voice_profiles.tsv').write_text('K:3\tNam\tBenchmark fixture\tTest\tTest\t1\tAdam\tKokoro\t3\t\tKể chuyện\nK:4\tNữ\tBenchmark fixture\tTest\tTest\t0\tBella\tKokoro\t4\t\tKể chuyện\n')
            v.ROOT=root;v.VALID=root/'validation'
            v.ENV=dict(original_env,MONO_PATH=str(v.TOOLS/'usr/lib/mono/4.5')+':'+str(root))
            v.compile('PerformanceBenchmark',['PerformanceBenchmark.cs'],main='PerformanceBenchmark',app=True)
            entry={'assembly_sha256':hashlib.sha256(assembly.read_bytes()).hexdigest(),'ui':[],'cache':[]}
            for run in range(3):
                v.ui(['PerformanceBenchmark'])
                text=(v.VALID/'PerformanceBenchmark.txt').read_text()
                fields=next(line for line in text.splitlines() if line.startswith('BENCH_UI|')).split('|')
                entry['version']=fields[1]
                entry['ui'].append(dict(ms=float(fields[2]),updates=int(fields[3]),files=int(fields[4]),queue_rebuilds=int(fields[5])))
                r=subprocess.run([v.MONO,str(root/'PerformanceBenchmark.exe'),'cache'],env=v.ENV,capture_output=True,text=True,timeout=60,check=True)
                fields=next(line for line in r.stdout.splitlines() if line.startswith('BENCH_CACHE|')).split('|')
                assert fields[1]==entry['version']
                entry['cache'].append(dict(ms=float(fields[2]),constructors=int(fields[3]),files=int(fields[4])))
            entry['median_ui_ms']=statistics.median(r['ms'] for r in entry['ui'])
            entry['median_cache_ms']=statistics.median(r['ms'] for r in entry['cache'])
            result[label]=entry
        assert result['updated']['version']=='4.1.0.0'
        result['scope']='Mono/Linux/Xvfb; actual UI/cache filesystem microbenchmarks, 3 runs each; not end-to-end TTS or RTX inference. Separate warm-worker integration uses synthetic Python TTS.'
        (original_valid/'performance410.json').write_text(json.dumps(result,indent=2))
        print(json.dumps(result,indent=2))
    finally:
        v.ROOT,v.VALID,v.ENV=original_root,original_valid,original_env
        shutil.rmtree(work)

if __name__=='__main__':main()
