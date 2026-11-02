"""Build Lite and test source-only rendering, legacy recovery and preserved TTS/UI behavior.
Speech/GPU providers are fixtures; FFmpeg, worker processes and WinForms are real.
"""
from pathlib import Path
import concurrent.futures, subprocess, tempfile, shutil, json, re, hashlib, datetime
import run_validation as v
CORE = ['BatchTests','TwoVoiceTests','VoiceReportTests','PunctuationTests','BreathStreamingTests',
        'TextTargetTests','AudioRepairTests','CommunityVoiceTests','VoiceDiagnosticTests',
        'AnchoredTests','NativeGpuTests','UpdaterTests','FastVoiceTests','Lite412Tests']
UI = ['Lite412UiTests','Lite412QueueUiTests']
def main():
    for name, files in [('V15VoiceStudio',v.APP),('V15Updater',['Updater.cs'])]:
        r=subprocess.run([v.MONO,v.MCS,'-target:winexe','-platform:x64',
            '-win32icon:'+str(v.SOURCE/'assets/V15VoiceStudio.ico'),
            '-resource:'+str(v.SOURCE/'assets/V15VoiceStudio.ico')+',V15VoiceStudio.Icon',
            '-out:'+str(v.ROOT/(name+'.exe'))]+v.REFS+[str(v.SOURCE/f) for f in files],
            env=v.ENV,capture_output=True,text=True)
        (v.VALID/(name+'-build.txt')).write_text(r.stdout+r.stderr)
        if r.returncode: raise RuntimeError(r.stdout+r.stderr)
    for name in CORE+UI:
        files=['Update141Tests.cs',name+'.cs'] if name=='BreathStreamingTests' else ['Updater.cs',name+'.cs'] if name=='UpdaterTests' else ['RenderPipelineFixture.cs',name+'.cs'] if name=='Lite412Tests' else [name+'.cs']
        try: v.compile(name,files,main=name,app=name!='UpdaterTests')
        except subprocess.CalledProcessError as e: raise RuntimeError(e.stdout+e.stderr)
    native=v.fixture()
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        jobs=[pool.submit(v.run,name,[str(native)] if name=='NativeGpuTests' else [],timeout=240) for name in CORE]
        for job in concurrent.futures.as_completed(jobs): job.result()
    oldroot,oldenv=v.ROOT,dict(v.ENV)
    ui=Path(tempfile.mkdtemp(prefix='beo412-lite-ui-'));(ui/'validation').mkdir()
    try:
        for name in ['V15VoiceStudio']+UI: shutil.copyfile(oldroot/(name+'.exe'),ui/(name+'.exe'))
        (ui/'voice_profiles.tsv').write_text('K:3\tNam\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t1\tAdam\tKokoro\t3\t\tKể chuyện\nK:4\tNữ\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t0\tBella\tKokoro\t4\t\tKể chuyện\n')
        for d in ['model/espeak-ng-data','engine']: (ui/d).mkdir(parents=True,exist_ok=True)
        for name in ['model.onnx','tokens.txt','voices.bin','lexicon-us-en.txt']: (ui/'model'/name).write_text('UI metadata fixture, never shipped')
        exe=ui/'engine/sherpa-onnx-offline-tts';exe.write_text('#!/bin/sh\ntouch '+str(ui/'auto-tts-called')+'\nexit 19\n');exe.chmod(0o755)
        v.ROOT=ui;v.ENV=dict(oldenv,MONO_PATH=str(v.TOOLS/'usr/lib/mono/4.5')+':'+str(ui),TZ='Asia/Ho_Chi_Minh')
        v.ui(UI)
        for p in (ui/'validation').glob('beo-*.png'):shutil.copyfile(p,v.VALID/p.name)
    finally: v.ROOT=oldroot;v.ENV=oldenv;shutil.rmtree(ui)
    for name in ['test_gpu_policy.py','test_worker_probe.py']:
        p=subprocess.run(['python3',str(v.SOURCE/name)],capture_output=True,text=True,timeout=30)
        (v.VALID/(name+'.txt')).write_text(p.stdout+p.stderr)
        if p.returncode:raise RuntimeError(p.stdout+p.stderr)
    counts={}
    for name in CORE+UI:
        result=(v.VALID/(name+'.txt')).read_text();m=re.search(r'TOTAL .*? (\d+) (?:PASS|passed)',result)
        if not m:raise RuntimeError('Missing test result for '+name)
        counts[name]=int(m.group(1))
    result={'version':'4.1.2.0','edition':'Lite','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'counts':counts,'CSharp_total':sum(counts.values()),'python_gpu_policy':11,'python_worker_probe':4,
        'binary_sha256':{name:hashlib.sha256((v.ROOT/name).read_bytes()).hexdigest() for name in ['V15VoiceStudio.exe','V15Updater.exe']},
        'environment':'Mono/Linux, real FFmpeg/processes and WinForms/Xvfb, synthetic speech/GPU providers',
        'real_windows_gpu_voice_clone_tested':False,'fresh_full_installer_ready':False,
        'missing_base_data':'V15VoiceStudio_1.4_Data.7z.001 through .015 not available'}
    (v.VALID/'completed412lite.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2),flush=True)
if __name__=='__main__':main()

