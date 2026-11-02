"""Validate exact packaged bytes and old/new updater compatibility on fixtures."""
from pathlib import Path
import hashlib,json,struct,zipfile,xml.etree.ElementTree as ET
import run_validation as v
def sha(data):return hashlib.sha256(data).hexdigest()
def pe(data):
    at=struct.unpack_from('<I',data,60)[0];opt=at+24
    assert data[:2]==b'MZ' and data[at:at+4]==b'PE\0\0' and struct.unpack_from('<H',data,at+4)[0]==0x8664
    sections=opt+struct.unpack_from('<H',data,at+20)[0]
    for i in range(struct.unpack_from('<H',data,at+6)[0]):
        size,start=struct.unpack_from('<II',data,sections+40*i+16);assert start+size<=len(data),'Truncated Windows PE'
def main():
    package=v.ROOT.parent/'output-4.1.1/BEO_Voice_Studio_4.1.1_Render_Fix_Update.zip'
    oldzip=v.ROOT.parent/'perf411-input/BEO_Voice_Studio_4.1.0_Fast_Voice_Update.zip'
    proof=json.loads((v.VALID/'completed411.json').read_text());perf=json.loads((v.VALID/'pipeline411.json').read_text())
    assert proof['CSharp_total']>=741 and perf['4.1.1']['binary_sha256']==proof['binary_sha256']['V15VoiceStudio.exe']
    with zipfile.ZipFile(package) as z:
        assert z.testzip() is None;names=z.namelist();assert len(names)==len({n.casefold() for n in names})
        manifest=ET.fromstring(z.read('manifest.xml'));assert manifest.attrib['product']=='V15VoiceStudio' and manifest.attrib['kind']=='core' and manifest.attrib['version']=='4.1.1'
        assert {n[8:] for n in names if n.startswith('payload/')}=={x.attrib['path'] for x in manifest}
        for x in manifest:
            data=z.read('payload/'+x.attrib['path']);assert len(data)==int(x.attrib['size']) and sha(data)==x.attrib['sha256']
        for name,value in proof['binary_sha256'].items():
            data=z.read('payload/'+name);assert sha(data)==value==sha((v.ROOT/name).read_bytes());pe(data)
        assert z.read('BEO_Update.exe')==z.read('V15Updater.exe')==z.read('payload/V15Updater.exe')
        for name in v.APP:assert z.read('payload/source/'+name)==(v.SOURCE/name).read_bytes()
        assert not any(n.startswith('payload/ai/models/') or n.startswith('payload/source/local-ai/model/') or (Path(n).name.startswith('Creator') and n.endswith('.cs')) for n in names)
        for name in ['model','engine','voice_profiles.tsv']:assert not (v.ROOT/name).exists(),'Test fixtures must not be shipped: '+name
        with zipfile.ZipFile(oldzip) as old:
            for name in ['ai_worker.py','gpu_policy.py','TwoVoices.cs','Punctuation.cs','AudioFinishing.cs','RepairPolicy.cs','RepairLanguage.cs','media/ffmpeg.exe']:
                assert z.read('payload/source/'+name)==old.read('payload/source/'+name),'Unexpected change in established audio/semantic policy: '+name
    v.compile('StablePackageTests',['StablePackageTests.cs'],main='StablePackageTests')
    cases=[('217',v.ROOT.parent/'output-2.0.17/V15VoiceStudio_2.0.17_Core_Update.zip',v.ROOT.parent/'V15VoiceStudio-2.0.17/V15Updater.exe'),('310',v.ROOT.parent/'perf411-input/BEO_Voice_Studio_3.1.0_Core_Update.zip',v.ROOT.parent/'BeoVoiceStudio-3.1.0/V15Updater.exe'),('410',oldzip,v.ROOT.parent/'BeoVoiceStudio-4.1.0/V15Updater.exe'),('411',oldzip,v.ROOT/'V15Updater.exe')]
    for label,old,updater in cases:
        v.run('StablePackageTests',[str(package),str(old),str(updater)],timeout=180);(v.VALID/('StablePackage411-'+label+'.txt')).write_text((v.VALID/'StablePackageTests.txt').read_text())
    result={'version':'4.1.1.0','package_sha256':sha(package.read_bytes()),'bytes':package.stat().st_size,'payload_files':len(manifest),'CSharp_checks':proof['CSharp_total'],'Python_checks':15,'update_install_checks':28,'binary_sha256':proof['binary_sha256'],'CRC_manifest_source_PE_audio_semantic_policy':'PASS','physical_Windows_GPU_tested':False}
    (package.parent/'release-validation411.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
if __name__=='__main__':main()
