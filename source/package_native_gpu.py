from pathlib import Path
import hashlib,json,xml.etree.ElementTree as ET,zipfile
from prepare_native_gpu import sha,DEST,ROOT

def main():
 assert (DEST/'info.json').exists(),'Complete dependency download and verification first'
 output=ROOT.parent/'output-1.4.3';output.mkdir(exist_ok=True)
 names=[f'V15_ONNX_GPU_1.4.3_{i:02d}.zip' for i in range(1,5)]
 archives=[zipfile.ZipFile(output/n,'w',zipfile.ZIP_DEFLATED,compresslevel=6) for n in names]
 manifest=ET.Element('update',product='V15VoiceStudio',version='1.4.3',kind='onnx',space=str(8*1024**3));piece_index=0
 try:
  for i,p in enumerate(sorted(DEST.iterdir())):
   if not p.is_file():continue
   node=ET.SubElement(manifest,'file',path='engine/gpu/'+p.name,size=str(p.stat().st_size),sha256=sha(p))
   with p.open('rb') as f:
    for j,chunk in enumerate(iter(lambda:f.read(32*1024**2),b'')):
     index=piece_index%4;entry=f'pieces/{i:03d}-{j:03d}.bin';piece_index+=1
     archives[index].writestr(entry,chunk)
     ET.SubElement(node,'piece',archive=names[index],entry=entry,size=str(len(chunk)),sha256=hashlib.sha256(chunk).hexdigest())
   print('Packed',p.name,flush=True)
  xml=ET.tostring(manifest,encoding='utf-8',xml_declaration=True)
  for a in archives:a.writestr('manifest.xml',xml)
 finally:
  for a in archives:a.close()
 print('READY',[(n,(output/n).stat().st_size) for n in names],flush=True)

if __name__=='__main__':main()
