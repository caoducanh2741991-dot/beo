"""Measure the supplied failing SRT with actual CTC before testing repair; no timing interpolation."""
import sys,re,json,wave
from pathlib import Path
import numpy as np
SOURCE=Path(__file__).parent;sys.path.insert(0,str(SOURCE/'asr'))
from asr_worker import dependencies,align_segments,merge_alignment_windows
root=SOURCE.parent.parent
p=root/'recovery/Wild Night Out P1 - Mr Ballen New Episode_2.srt'
def seconds(t):
 h,m,s,ms=map(int,re.split('[:,]',t));return h*3600+m*60+s+ms/1000
segments=[]
for b in re.split(r'\n\s*\n',p.read_text(encoding='utf-8-sig').strip()):
 lines=b.splitlines();a,z=lines[1].split(' --> ');segments.append(dict(start=seconds(a),end=seconds(z),text=' '.join(lines[2:])))
with wave.open('/tmp/v149-source.wav') as f:audio=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype('float32')/32768
dependencies();words=align_segments(audio,merge_alignment_windows(segments),root/'asr-validation-data','cpu')
out=SOURCE.parent/'validation/user-srt-aligned-words.json';out.write_text(json.dumps(words,indent=2));print('DONE',len(words),flush=True)
