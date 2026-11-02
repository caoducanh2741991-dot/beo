"""Reproducible 2.0.15 Windows build + Mono/Linux contract tests.
Requires Mono + .NET 4.5 reference assemblies + FFmpeg + Xvfb.
Set V15_BUILD_TOOLS and V15_NET45_REFS as described in the release guide.
The injected voices/rewriter are synthetic; this does not validate real GPU/TTS.
"""
import sys,subprocess,concurrent.futures
from pathlib import Path
import run_validation as v
CORE=['StableRepairTests','VanRegressionTests','BatchTests','TwoVoiceTests','VoiceReportTests','PunctuationTests','BreathStreamingTests','TextTargetTests','AudioRepairTests','CommunityVoiceTests','VoiceDiagnosticTests','LocalAiSafetyTests','AnchoredTests']
UI=['StableRepairUiTests','StudioLayoutTests']
def main():
 for name,files in [('V15VoiceStudio',v.APP),('V15Updater',['Updater.cs'])]:
  command=[v.MONO,v.MCS,'-target:winexe','-win32icon:'+str(v.SOURCE/'assets/V15VoiceStudio.ico'),'-resource:'+str(v.SOURCE/'assets/V15VoiceStudio.ico')+',V15VoiceStudio.Icon','-platform:x64','-out:'+str(v.ROOT/(name+'.exe'))]+v.REFS+[str(v.SOURCE/f) for f in files]
  subprocess.run(command,env=v.ENV,check=True)
 for name in CORE+UI+['NativeGpuTests']:
  files=[name+'.cs'] if name!='BreathStreamingTests' else ['Update141Tests.cs',name+'.cs']
  v.compile(name,files,main=name,app=True)
 v.compile('UpdaterTests',['Updater.cs','UpdaterTests.cs'],main='UpdaterTests')
 native=v.fixture();failures=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
  jobs={pool.submit(v.run,name,[str(native)] if name=='NativeGpuTests' else [],timeout=240):name for name in CORE+['NativeGpuTests','UpdaterTests']}
  for job in concurrent.futures.as_completed(jobs):
   try:job.result()
   except Exception as exc:failures.append((jobs[job],str(exc)))
 if failures:
  for name,error in failures:print(name,error)
  raise SystemExit(1)
 v.ui(UI)
 print('2.0.15 CONTRACT VALIDATION COMPLETE; physical Windows/RTX2070 and real cloned TTS are NOT tested.')
if __name__=='__main__':main()
