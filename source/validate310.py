"""Build the actual Windows EXEs and exercise two-mode output/UI contracts.
Tests execute on Mono/Linux with real FFmpeg and synthetic speech/providers.
Physical Windows, installed clone models and RTX 2070 are not claimed tested.
"""
import concurrent.futures,subprocess,sys
from pathlib import Path
import run_validation as v
CORE=['Reno300Tests','Acoustic300Tests','RenderProgressTests','Workflow217Tests','Recovery216Tests','Sync216Tests','BatchTests','TwoVoiceTests','VoiceReportTests','PunctuationTests','BreathStreamingTests','TextTargetTests','AudioRepairTests','CommunityVoiceTests','VoiceDiagnosticTests','LocalAiSafetyTests','AnchoredTests','NativeGpuTests','UpdaterTests']
def main():
 for name,files in [('V15VoiceStudio',v.APP),('V15Updater',['Updater.cs'])]:
  subprocess.run([v.MONO,v.MCS,'-target:winexe','-platform:x64','-win32icon:'+str(v.SOURCE/'assets/V15VoiceStudio.ico'),'-resource:'+str(v.SOURCE/'assets/V15VoiceStudio.ico')+',V15VoiceStudio.Icon','-out:'+str(v.ROOT/(name+'.exe'))]+v.REFS+[str(v.SOURCE/f) for f in files],env=v.ENV,check=True)
 for name in CORE+['BeoUiTests']:
  files=['Recovery216Tests.cs','VanRegressionTests.cs'] if name=='Recovery216Tests' else ['Update141Tests.cs',name+'.cs'] if name=='BreathStreamingTests' else ['Updater.cs',name+'.cs'] if name=='UpdaterTests' else [name+'.cs']
  try:v.compile(name,files,main=name,app=name!='UpdaterTests')
  except subprocess.CalledProcessError as error:print(error.stdout,error.stderr);raise
 failures=[];native=v.fixture()
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
  jobs={pool.submit(v.run,name,[str(native)] if name=='NativeGpuTests' else [],timeout=300):name for name in CORE}
  for job in concurrent.futures.as_completed(jobs):
   try:job.result()
   except Exception as error:failures.append((jobs[job],str(error)))
 if failures:
  for name,error in failures:print(name,error)
  raise SystemExit(1)
 reno=(v.VALID/'reno300-root.txt').read_text()
 replay_log=(v.VALID/'Reno300Tests.txt').read_text()
 v.run('Reno300Tests',['reopen',reno],timeout=120)
 (v.VALID/'Reno300Tests-reopen.txt').write_text((v.VALID/'Reno300Tests.txt').read_text())
 (v.VALID/'Reno300Tests.txt').write_text(replay_log)
 saved=(v.VALID/'recovery-root.txt').read_text()
 r=subprocess.run([v.MONO,str(v.ROOT/'Recovery216Tests.exe'),'reopen',saved],env=v.ENV,capture_output=True,text=True,timeout=180)
 (v.VALID/'Recovery216Tests-reopen.txt').write_text(r.stdout+r.stderr)
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 print('Separate process:',r.stdout.strip().splitlines()[-1],flush=True)
 catalogue=v.ROOT/'voice_profiles.tsv';before=catalogue.read_bytes() if catalogue.exists() else None
 catalogue.write_text('K:3\tNam\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t1\tAdam\tKokoro\t3\t\tKể chuyện\nK:4\tNữ\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t0\tBella\tKokoro\t4\t\tKể chuyện\n')
 v.ENV['TZ']='Asia/Ho_Chi_Minh'
 try:v.ui(['BeoUiTests'])
 finally:
  if before is None:catalogue.unlink()
  else:catalogue.write_bytes(before)
 for name in ['test_gpu_policy.py','test_worker_probe.py']:
  r=subprocess.run([sys.executable,str(v.SOURCE/name)],capture_output=True,text=True,timeout=30);(v.VALID/(name+'.txt')).write_text(r.stdout+r.stderr)
  if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 import json,hashlib,datetime
 counts={}
 for name in CORE+['Reno300Tests-reopen','Recovery216Tests-reopen','BeoUiTests']:
  path=v.VALID/(name+'.txt');data=path.read_text();import re
  marker=re.search(r'TOTAL .*? (\d+) (?:PASS|passed)',data)
  if marker:counts[name]=int(marker.group(1))
 summary={'version':'3.1.0.0','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'counts':counts,'CSharp_total':sum(counts.values()),'python_gpu_policy':11,'python_worker_probe':4,'binary_sha256':{f:hashlib.sha256((v.ROOT/f).read_bytes()).hexdigest() for f in ['V15VoiceStudio.exe','V15Updater.exe']},'environment':'Mono/Linux, Xvfb and real FFmpeg; synthetic TTS/AI fixtures; missing-engine UI cases','physical_Windows_or_GPU_or_real_clone_tested':False}
 (v.VALID/'completed310.json').write_text(json.dumps(summary,indent=2))
 print('3.1.0 CONTRACTS COMPLETE. Mono/Linux; physical Windows, selected clone and RTX2070 remain untested.',flush=True)
if __name__=='__main__':main()
