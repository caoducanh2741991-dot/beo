from pathlib import Path
import re,json,hashlib,xml.etree.ElementTree as E,difflib
root=Path(__file__).resolve().parent.parent;base=root.parent/'baseline-1.4.8/payload/source';x=E.parse(root/'validation/refined-user.xml').getroot();cs=x.findall('cue')
def norm(s):return re.findall(r"[a-z0-9]+(?:'[a-z]+)?",s.lower())
old=[]
for b in re.split(r'\n\s*\n',(root.parent/'recovery/Wild Night Out P1 - Mr Ballen New Episode_2.srt').read_text(encoding='utf-8-sig').strip()):old.append(' '.join(b.splitlines()[2:]))
a,b=norm(' '.join(old)),norm(' '.join(c.text for c in cs));changes=[]
for tag,i,j,k,l in difflib.SequenceMatcher(None,a,b,autojunk=False).get_opcodes():
 if tag!='equal':changes.append(dict(tag=tag,old=a[i:j],new=b[k:l]))
assert len(changes)==2 and all(c['tag']=='delete' and not c['new'] for c in changes),changes
assert sorted(c['old'][0] for c in changes)==["he's",'the'],changes
prev=0
for c in cs:
 start,end=float(c.get('start')),float(c.get('end'));assert start>=prev-.0005 and end>start and end<=float(x.get('duration'))+.0005;prev=end
summary=dict(cues=len(cs),words=sum(len(c.text.split()) for c in cs),short=sum(float(c.get('end'))-float(c.get('start'))<1 for c in cs),fast=sum(len(c.text.replace('\n',' '))/(float(c.get('end'))-float(c.get('start')))>25 for c in cs),long_lines=sum(any(len(l)>42 for l in c.text.splitlines()) for c in cs),pending=sum(bool(c.get('note')) for c in cs),changes=changes,phrases={p:any(p in c.text.replace('\n',' ') for c in cs) for p in ['smelled like vomit','throws it','being gone']},audit=[c.attrib for c in x.findall('audit/change')])
assert summary['words']==1047 and summary['short']==0 and summary['long_lines']==0 and summary['fast']<=5 and all(summary['phrases'].values()),summary
for f in ['fresh-short.xml','delayed.xml']:
 t=E.parse(root/'validation'/f).getroot();last=0
 for c in t.findall('cue'):assert float(c.get('start'))>=last-.0005 and float(c.get('end'))>float(c.get('start'));last=float(c.get('end'))
summary['delayed_first']=float(E.parse(root/'validation/delayed.xml').find('cue').get('start'));assert 2.45<=summary['delayed_first']<=2.75
unchanged={}
for name in ['ai_worker.py','gpu_policy.py','NativeGpu.cs','ExternalSpeech.cs','Core.cs','VoiceCatalog.cs']:
 content=(root/'source'/name).read_bytes();assert content==(base/name).read_bytes(),name;unchanged[name]=hashlib.sha256(content).hexdigest()
(root/'validation/unchanged-tts-gpu.json').write_text(json.dumps(unchanged,indent=2));(root/'validation/completion-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
