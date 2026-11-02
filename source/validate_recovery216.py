"""Build Windows x64 EXEs; run synthetic contracts with real FFmpeg on Mono/Linux.
Real Qwen CPU checks are opt-in (RealQwen216Tests; pinned installed model required).
No test here asserts physical Windows, RTX2070, or selected cloned voice quality.
"""
import sys,subprocess,concurrent.futures
from pathlib import Path
import run_validation as v
CORE=['Recovery216Tests','Sync216Tests','BatchTests','TwoVoiceTests','VoiceReportTests','PunctuationTests','BreathStreamingTests','TextTargetTests','AudioRepairTests','CommunityVoiceTests','VoiceDiagnosticTests','LocalAiSafetyTests','AnchoredTests']
UI=['StableRepairUiTests','StudioLayoutTests','AudioRepairUiTests']
def main():
 for name,files in [('V15VoiceStudio',v.APP),('V15Updater',['Updater.cs'])]:
  subprocess.run([v.MONO,v.MCS,'-target:winexe','-platform:x64','-win32icon:'+str(v.SOURCE/'assets/V15VoiceStudio.ico'),'-resource:'+str(v.SOURCE/'assets/V15VoiceStudio.ico')+',V15VoiceStudio.Icon','-out:'+str(v.ROOT/(name+'.exe'))]+v.REFS+[str(v.SOURCE/f) for f in files],env=v.ENV,check=True)
 for name in CORE+UI+['NativeGpuTests','RealQwen216Tests']:
  files=['Recovery216Tests.cs','VanRegressionTests.cs'] if name=='Recovery216Tests' else ['AudioRepairTests.cs',name+'.cs'] if name=='AudioRepairUiTests' else [name+'.cs'] if name!='BreathStreamingTests' else ['Update141Tests.cs',name+'.cs']
  try:v.compile(name,files,main=name,app=True)
  except subprocess.CalledProcessError as exc:print(exc.stdout,exc.stderr);raise
 v.compile('UpdaterTests',['Updater.cs','UpdaterTests.cs'],main='UpdaterTests')
 failures=[];native=v.fixture()
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
  jobs={pool.submit(v.run,name,[str(native)] if name=='NativeGpuTests' else [],timeout=300):name for name in CORE+['NativeGpuTests','UpdaterTests']}
  for job in concurrent.futures.as_completed(jobs):
   try:job.result()
   except Exception as exc:failures.append((jobs[job],str(exc)))
 if failures:
  for name,error in failures:print(name,error)
  raise SystemExit(1)
 path=(v.VALID/'recovery-root.txt').read_text()
 r=subprocess.run([v.MONO,str(v.ROOT/'Recovery216Tests.exe'),'reopen',path],env=v.ENV,capture_output=True,text=True,timeout=180)
 (v.VALID/'Recovery216Tests-reopen.txt').write_text(r.stdout+r.stderr)
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 print('Separate process recovery',r.stdout.strip().splitlines()[-1],flush=True)
 catalogue=v.ROOT/'voice_profiles.tsv';before=catalogue.read_bytes() if catalogue.exists() else None
 if before is None:catalogue.write_text("K:3\tNam\tDữ liệu kiểm thử giao diện\tUI fixture\tTest\t1\tAdam\tKokoro\t3\t\tKể chuyện\n",encoding='utf-8')
 try:v.ui(UI)
 finally:
  if before is None:catalogue.unlink()
  else:catalogue.write_bytes(before)
 for name in ['test_gpu_policy.py','test_worker_probe.py']:
  r=subprocess.run([sys.executable,str(v.SOURCE/name)],capture_output=True,text=True,timeout=30);(v.VALID/(name+'.txt')).write_text(r.stdout+r.stderr)
  if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 print('2.0.16 CONTRACTS COMPLETE. Physical Windows/RTX2070 and selected cloned TTS are NOT validated.')
if __name__=='__main__':main()
