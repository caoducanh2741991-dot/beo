"""English phrase boundaries using a bundled, hash-checked local POS model."""
import hashlib,json,re,zipfile
from pathlib import Path
_TAGGER=None
PRON=set('i you he she it we they me him her us them'.split())
DET=set('a an the my your his her our their its this that these those'.split())
PREP=set('like to of in on at for with from into onto over under through around about without within across toward towards behind beside between before after'.split())
AUX=set('am is are was were be been being have has had having do does did will would can could shall should may might must'.split())
def tagger():
 global _TAGGER
 if _TAGGER is None:
  from nltk.tag.perceptron import PerceptronTagger
  p=Path(__file__).with_name('phrase_model')/'averaged_perceptron_tagger_eng.zip'
  if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest()!='6025f530624335c67d6547d44757b357b4e79bae030a0383e9887a92c1718f0b':raise RuntimeError('Thiếu/hỏng mô hình chia cụm tiếng Anh. Cài lại ZIP cập nhật 1.4.9.')
  t=PerceptronTagger(load=False)
  with zipfile.ZipFile(p) as z:
   prefix='averaged_perceptron_tagger_eng/averaged_perceptron_tagger_eng.'
   t.model.weights=json.loads(z.read(prefix+'weights.json'));t.tagdict=json.loads(z.read(prefix+'tagdict.json'));t.classes=set(json.loads(z.read(prefix+'classes.json')));t.model.classes=t.classes
  _TAGGER=t
 return _TAGGER

def boundary_rules(units):
 tokens=[];owners=[]
 for i,u in enumerate(units):
  for w in u['word'].split():tokens.append(w);owners.append(i)
 body=[re.sub(r'^[\"\u201c]+|[,.!?;:\"\u201d]+$','',w).replace('\u2019',"'") for w in tokens]
 tags=[p[1] for p in tagger().tag(body)];bad={}
 for i in range(1,len(tokens)):
  if owners[i]==owners[i-1] or re.search(r'[,;:.!?][\"\u201d\']?$',tokens[i-1]):continue
  left,right=body[i-1].lower(),body[i].lower();a,b=tags[i-1],tags[i];reason=None
  if left in PREP and (b.startswith(('NN','JJ','CD')) or right in DET or right in PRON):reason='giới từ và bổ ngữ'
  elif (left in DET or a in {'DT','PRP$','POS','JJ','JJR','JJS','CD'}) and b.startswith(('NN','JJ','CD')):reason='cụm danh từ'
  elif a.startswith('NN') and b.startswith('NN'):reason='tên / cụm danh từ'
  elif left in AUX and (b.startswith(('VB','JJ','RB')) or right in AUX):reason='trợ động từ và vị ngữ'
  elif a.startswith('RB') and b.startswith(('VB','JJ','RB')):reason='trạng từ và từ bổ nghĩa'
  elif a.startswith('VB') and (right in PRON or right in DET or right in PREP):reason='động từ và bổ ngữ'
  elif left in PRON and (b.startswith('VB') or right in AUX):reason='chủ ngữ và động từ'
  if reason:bad[owners[i]]=reason
 return bad
