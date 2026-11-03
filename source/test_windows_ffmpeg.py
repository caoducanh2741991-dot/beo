"""Check the exact bundled Windows binary, tempo filters and loudness analysis."""
from pathlib import Path
import hashlib,json,math,os,struct,subprocess,wave
ROOT=Path(__file__).resolve().parent.parent
def main():
 if os.name!='nt':raise RuntimeError('A Windows binary must be tested on Windows')
 out=ROOT/'windows-audio-proof';out.mkdir(exist_ok=True)
 source=out/'input.wav'
 with wave.open(str(source),'wb') as w:
  w.setparams((1,2,24000,0,'NONE','PCM'))
  w.writeframes(b''.join(struct.pack('<h',int(8000*math.sin(i*2*math.pi*220/24000))) for i in range(96000)))
 exe=ROOT/'source/media/ffmpeg.exe';results=[]
 for speed in [.9,1.1]:
  target=out/('tempo-'+str(speed)+'.wav')
  run=subprocess.run([str(exe),'-nostdin','-v','error','-y','-i',str(source),'-af','atempo='+str(speed),'-ac','1','-ar','24000','-c:a','pcm_s16le',str(target)],capture_output=True,text=True,timeout=30)
  if run.returncode:raise RuntimeError(run.stderr)
  with wave.open(str(target)) as w:seconds=w.getnframes()/w.getframerate();raw=w.readframes(w.getnframes())
  if abs(seconds-4/speed)>.15:raise RuntimeError('Incorrect measured atempo duration')
  if max(abs(x[0]) for x in struct.iter_unpack('<h',raw))<100:raise RuntimeError('Silent converted waveform')
  results.append({'speed':speed,'measured_seconds':seconds})
 run=subprocess.run([str(exe),'-nostdin','-i',str(source),'-af','loudnorm=I=-18:TP=-1:LRA=11:print_format=json','-f','null','-'],capture_output=True,text=True,timeout=30)
 if run.returncode or 'input_i' not in run.stderr:raise RuntimeError('Bundled loudnorm analysis failed: '+run.stderr)
 (out/'windows-audio-validation.json').write_text(json.dumps({'checks':5,'status':'PASS','actual_windows_ffmpeg':True,'ffmpeg_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'tempo':results,'loudnorm_analysis':True},indent=2))
 print('TOTAL WINDOWS FFMPEG 5 PASS')
if __name__=='__main__':main()
