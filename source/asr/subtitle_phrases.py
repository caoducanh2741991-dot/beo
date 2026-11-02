"""English phrase boundaries using a bundled, hash-checked local POS model."""
import hashlib,json,re,zipfile
from pathlib import Path
_TAGGER=None
PRON=set("i you he she it we they me him her us them who's he's she's it's we're they're i'm you're".split())
DET=set('a an the my your his her our their its this that these those'.split())
PREP=set('like to of in on at for with from into onto over under through around about without within across toward towards behind beside between before after'.split())
AUX=set('am is are was were be been being have has had having do does did will would can could shall should may might must'.split())
CATENATIVE=set('begin begins began begun start starts started starting stop stops stopped stopping keep keeps kept continue continues continued continuing finish finishes finished finishing avoid avoids avoided enjoy enjoys enjoyed'.split())
LINKING=set('look looks looked looking seem seems seemed feel feels felt smell smells smelled sound sounds sounded become becomes became remain remains remained'.split())
def tagger():
 global _TAGGER
 if _TAGGER is None:
  from nltk.tag.perceptron import PerceptronTagger
  p=Path(__file__).with_name('phrase_model')/'averaged_perceptron_tagger_eng.zip'
  if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest()!='6025f530624335c67d6547d44757b357b4e79bae030a0383e9887a92c1718f0b':raise RuntimeError('Thiếu/hỏng mô hình chia cụm tiếng Anh. Cài lại ZIP cập nhật 1.4.10.')
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
  if owners[i]==owners[i-1] or re.search(r'[;:.!?][\"\u201d\']?$',tokens[i-1]):continue
  left,right=body[i-1].lower(),body[i].lower();a,b=tags[i-1],tags[i];reason=None
  # A comma is normally a legal phrase boundary, except inside speech fillers
  # such as "he's, like". Stops always remain legal.
  if tokens[i-1].endswith(',') and not (left in PRON and right=='like'):continue
  if left in PREP and (b.startswith(('NN','JJ','CD')) or right in DET or right in PRON):reason='giới từ và bổ ngữ'
  elif (left in DET or a in {'DT','PRP$','POS','JJ','JJR','JJS','CD'}) and b.startswith(('NN','JJ','CD')):reason='cụm danh từ'
  elif left in DET and b.startswith('RB') and i+1<len(tags) and tags[i+1].startswith(('NN','JJ')):reason='từ hạn định và cụm danh từ có trạng từ'
  elif a.startswith('NN') and b.startswith('NN'):reason='tên / cụm danh từ'
  elif left in AUX and (b.startswith(('VB','JJ','RB')) or right in AUX):reason='trợ động từ và vị ngữ'
  elif a.startswith('RB') and b.startswith(('VB','JJ','RB')):reason='trạng từ và từ bổ nghĩa'
  elif a.startswith('VB') and (right in PRON or right in DET or right in PREP):reason='động từ và bổ ngữ'
  elif left in PRON and (b.startswith('VB') or right in AUX):reason='chủ ngữ và động từ'
  elif left in PRON and right=='like':reason='cụm đệm hội thoại với like'
  elif a.startswith('NN') and (b.startswith('VB') or right in AUX):reason='chủ ngữ danh từ và vị ngữ'
  elif left in CATENATIVE and b.startswith('VB'):reason='động từ nối và hành động'
  elif left in LINKING and b.startswith(('JJ','VBN')):reason='động từ nối và tính chất'
  elif left in {'which','who'} and (right in AUX or b.startswith('VB')):reason='đại từ quan hệ và vị ngữ'
  elif left in {'he','she','it','they','we'} and b.startswith('RB'):reason='chủ ngữ và trạng từ của vị ngữ'
  elif left in {'there','here'} and (right in AUX or b.startswith('VB')):reason='cấu trúc there / here'
  elif right in {'there','here'} and a.startswith('VB'):reason='động từ và bổ ngữ nơi chốn'
  elif left in {'rest','part','end','side','way'} and right=='of':reason='cụm danh từ và of'
  elif left in PREP and right in {'all','both'}:reason='giới từ và cụm bổ ngữ'
  if reason:bad[owners[i]]=reason
 return bad
