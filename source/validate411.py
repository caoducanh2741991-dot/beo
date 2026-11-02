"""Build 4.1.1 and run the preserved contracts plus full pipeline recovery tests.

Mono/Linux, actual FFmpeg and WinForms/Xvfb; synthetic providers, no physical
Windows/RTX inference. UI fixtures live in an isolated temporary app folder.
"""
import concurrent.futures,subprocess,sys,shutil,tempfile,json,hashlib,datetime,re
from pathlib import Path
import run_validation as v
CORE=['Reno300Tests','Acoustic300Tests','RenderProgressTests','Workflow217Tests','Recovery216Tests','Sync216Tests','BatchTests','TwoVoiceTests','VoiceReportTests','PunctuationTests','BreathStreamingTests','TextTargetTests','AudioRepairTests','CommunityVoiceTests','VoiceDiagnosticTests','LocalAiSafetyTests','AnchoredTests','NativeGpuTests','UpdaterTests']
MORE=['FastVoiceTests','Render411Tests','FastVoiceUiTests','BeoUiTests','Render411UiTests','Render411ReopenTests']

def main():
    (v.VALID/'completed411.json').unlink(missing_ok=True)
    for name,files in [('V15VoiceStudio',v.APP),('V15Updater',['Updater.cs'])]:
        subprocess.run([v.MONO,v.MCS,'-target:winexe','-platform:x64','-win32icon:'+str(v.SOURCE/'assets/V15VoiceStudio.ico'),'-resource:'+str(v.SOURCE/'assets/V15VoiceStudio.ico')+',V15VoiceStudio.Icon','-out:'+str(v.ROOT/(name+'.exe'))]+v.REFS+[str(v.SOURCE/f) for f in files],env=v.ENV,check=True)
    for name in CORE+MORE:
        files=['Recovery216Tests.cs','VanRegressionTests.cs'] if name=='Recovery216Tests' else ['Update141Tests.cs',name+'.cs'] if name=='BreathStreamingTests' else ['Updater.cs',name+'.cs'] if name=='UpdaterTests' else ['RenderPipelineFixture.cs',name+'.cs'] if name in ['Render411Tests','Render411ReopenTests'] else [name+'.cs']
        try:v.compile(name,files,main=name,app=name!='UpdaterTests')
        except subprocess.CalledProcessError as e:print(e.stdout,e.stderr);raise
    native=v.fixture();failures=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        jobs={pool.submit(v.run,name,[str(native)] if name=='NativeGpuTests' else [],timeout=300):name for name in CORE}
        for job in concurrent.futures.as_completed(jobs):
            try:job.result()
            except Exception as e:failures.append((jobs[job],str(e)))
    if failures:
        for name,error in failures:print(name,error)
        raise SystemExit(1)
    for name in ['FastVoiceTests','Render411Tests']:v.run(name,timeout=180)
    v.run('Render411ReopenTests',[(v.VALID/'render411-checkpoint-root.txt').read_text()],timeout=90)
    saved=(v.VALID/'reno300-root.txt').read_text();log=(v.VALID/'Reno300Tests.txt').read_text();v.run('Reno300Tests',['reopen',saved],timeout=120);(v.VALID/'Reno300Tests-reopen.txt').write_text((v.VALID/'Reno300Tests.txt').read_text());(v.VALID/'Reno300Tests.txt').write_text(log)
    saved=(v.VALID/'recovery-root.txt').read_text();r=subprocess.run([v.MONO,str(v.ROOT/'Recovery216Tests.exe'),'reopen',saved],env=v.ENV,capture_output=True,text=True,timeout=180);(v.VALID/'Recovery216Tests-reopen.txt').write_text(r.stdout+r.stderr)
    if r.returncode:raise RuntimeError(r.stdout+r.stderr)
    print('Recovery in a separate process: PASS',flush=True)
    original_root,original_env=v.ROOT,dict(v.ENV);uiroot=Path(tempfile.mkdtemp(prefix='beo411-ui-app-'));(uiroot/'validation').mkdir()
    try:
        for name in ['V15VoiceStudio','FastVoiceUiTests','BeoUiTests','Render411UiTests']:shutil.copyfile(original_root/(name+'.exe'),uiroot/(name+'.exe'))
        (uiroot/'voice_profiles.tsv').write_text('K:3\tNam\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t1\tAdam\tKokoro\t3\t\tKể chuyện\nK:4\tNữ\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t0\tBella\tKokoro\t4\t\tKể chuyện\n')
        v.ROOT=uiroot;v.ENV=dict(original_env,MONO_PATH=str(v.TOOLS/'usr/lib/mono/4.5')+':'+str(uiroot),TZ='Asia/Ho_Chi_Minh')
        v.ui(['BeoUiTests'])
        for d in ['model/espeak-ng-data','engine']:(uiroot/d).mkdir(parents=True,exist_ok=True)
        for name in ['model.onnx','tokens.txt','voices.bin','lexicon-us-en.txt']:(uiroot/'model'/name).write_text('UI metadata readiness fixture')
        exe=uiroot/'engine/sherpa-onnx-offline-tts';exe.write_text('#!/bin/sh\ntouch '+str(uiroot/'auto-tts-called')+'\nexit 19\n');exe.chmod(0o755)
        v.ui(['FastVoiceUiTests','Render411UiTests'])
        for p in (uiroot/'validation').glob('beo-*.png'):shutil.copyfile(p,v.VALID/p.name)
    finally:v.ROOT=original_root;v.ENV=original_env;shutil.rmtree(uiroot)
    for name in ['test_gpu_policy.py','test_worker_probe.py']:
        r=subprocess.run([sys.executable,str(v.SOURCE/name)],capture_output=True,text=True,timeout=30);(v.VALID/(name+'.txt')).write_text(r.stdout+r.stderr)
        if r.returncode:raise RuntimeError(r.stdout+r.stderr)
    counts={}
    for name in CORE+MORE+['Reno300Tests-reopen','Recovery216Tests-reopen']:
        text=(v.VALID/(name+'.txt')).read_text();marker=re.search(r'TOTAL .*? (\d+) (?:PASS|passed)',text);assert marker,name;counts[name]=int(marker.group(1))
    summary={'version':'4.1.1.0','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'counts':counts,'CSharp_total':sum(counts.values()),'python_gpu_policy':11,'python_worker_probe':4,'binary_sha256':{f:hashlib.sha256((v.ROOT/f).read_bytes()).hexdigest() for f in ['V15VoiceStudio.exe','V15Updater.exe']},'environment':'Mono/Linux; actual FFmpeg, WinForms/Xvfb, file hashes and external processes; synthetic speech/AI, no real GPU/model inference','physical_Windows_or_GPU_or_real_clone_tested':False}
    (v.VALID/'completed411.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2),flush=True)

if __name__=='__main__':main()
