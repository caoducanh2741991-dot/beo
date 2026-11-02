"""Acceptance on actual user media outputs, not fabricated subtitle timestamps."""
from pathlib import Path
import xml.etree.ElementTree as E
import re,json,hashlib,sys
ROOT=Path(__file__).resolve().parent.parent
norm=lambda t:re.sub(r'[^a-z0-9\s]','',t.lower().replace('\n',' '))
def text(r):return ' '.join(' '.join((x.text or '').split()) for x in r.findall('cue'))
def metrics(path):
 r=E.parse(path).getroot();cs=r.findall('cue');duration=float(r.get('duration'));last=0
 assert r.get('version')=='1.4.10' and r.get('pipeline')=='v1410-speech-1' and cs
 for c in cs:
  a,b=float(c.get('start')),float(c.get('end'));assert 0<=a<b<=duration+.0005 and a>=last-.0005;last=b
  assert len(c.text.splitlines())<=2 and all(len(l)<=42 for l in c.text.splitlines())
 return r,dict(cues=len(cs),words=len(text(r).split()),under1=sum(float(c.get('end'))-float(c.get('start'))<.9995 for c in cs),over25CPS=sum(len(' '.join(c.text.split()))/(float(c.get('end'))-float(c.get('start')))>25 for c in cs),review=sum(bool(c.get('note')) for c in cs),tail_adjustments=sum(w.get('ctc_end_before') is not None for w in r.findall('words/word')),independent_asr_anchors=sum(w.get('asr_end') is not None for w in r.findall('words/word')))
def main():
 result={}
 for name in ['fresh-video.xml','refined-video.xml','fresh-short.xml','delayed.xml','refined-clean-short.xml','quality-large-short.xml']:
  r,m=metrics(ROOT/'validation'/name);result[name]=m
  if name!='refined-clean-short.xml':assert r.get('repetitions')=='preserve' and not r.findall('audit/change')
  else:
   assert r.get('repetitions')=='clean';assert "he's he" in text(r).lower(),text(r)
  if name in ['fresh-video.xml','refined-video.xml']:
   assert m['under1']==0
   phrases=['he begins walking','John goes','Frankie had gone','Frankie looks',"he's like",'he actually began driving','the rest of the way','He eventually','how he got there','then there were','smelled like vomit','throws it','being gone']
   body=[norm(c.text) for c in r.findall('cue')];missing=[p for p in phrases if not any(norm(p) in c for c in body)]
   result[name]['missing_reported_phrases']=missing;assert not missing,(name,missing)
   assert m['independent_asr_anchors']>500
  if name=='quality-large-short.xml':assert r.get('model')=='large-v3'
  if name=='delayed.xml':assert 2.25<=float(r.find('cue').get('start'))<=2.75
 draft=E.parse(ROOT/'validation/uploaded149-draft.xml').getroot();refined=E.parse(ROOT/'validation/refined-video.xml').getroot()
 assert norm(text(draft))==norm(text(refined)),'Refine must not silently remove a spoken word'
 assert 'the the' in text(refined).lower(),'Default must preserve the actual uploaded repetition'
 fresh=E.parse(ROOT/'validation/fresh-video.xml').getroot();cache=Path(sys.argv[1]);data=json.loads(cache.read_text());assert data['schema']=='v1410-speech-1' and data['repetitions']=='preserve'
 assert norm(text(fresh))==norm(' '.join(s['text'] for s in data['segments'])),'Fresh assembly lost primary-recognition wording'
 result['refine_exact_word_preservation']=True;result['fresh_exact_word_preservation']=True
 unchanged=[]
 for name in ['ai_worker.py','gpu_policy.py','NativeGpu.cs','ExternalSpeech.cs','Core.cs','VoiceCatalog.cs']:
  data=(ROOT/'source'/name).read_bytes();assert data==(ROOT.parent/'V15VoiceStudio-1.4.9/source'/name).read_bytes();unchanged.append(name)
 result['unchanged_tts_gpu_files']=unchanged
 (ROOT/'validation/completion-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2));print(json.dumps(result,ensure_ascii=False,indent=2));print('COMPLETION 1.4.10 PASS')
if __name__=='__main__':main()
