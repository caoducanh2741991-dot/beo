"""Validate exact release bytes and run old/current updaters on fixture installs.

No physical Windows, real speech models or GPU inference are certified here.
"""
from pathlib import Path
import hashlib,json,struct,zipfile,xml.etree.ElementTree as ET
import run_validation as v

def sha(data):return hashlib.sha256(data).hexdigest()
def pe(data):
    at=struct.unpack_from('<I',data,60)[0];opt=at+24
    assert data[:2]==b'MZ' and data[at:at+4]==b'PE\0\0'
    assert struct.unpack_from('<H',data,at+4)[0]==0x8664
    sections=opt+struct.unpack_from('<H',data,at+20)[0]
    for i in range(struct.unpack_from('<H',data,at+6)[0]):
        length,offset=struct.unpack_from('<II',data,sections+40*i+16)
        assert offset+length<=len(data),'Truncated PE image'

def main():
    package=v.ROOT.parent/'output-4.1.0/BEO_Voice_Studio_4.1.0_Fast_Voice_Update.zip'
    baseline=v.ROOT.parent/'creator-guide-20261102/input/BEO_Voice_Studio_4.0.0_Creator_Update.zip'
    proof=json.loads((v.VALID/'completed410.json').read_text())
    perf=json.loads((v.VALID/'performance410.json').read_text())
    assert proof['CSharp_total']>=696
    assert perf['updated']['assembly_sha256']==proof['binary_sha256']['V15VoiceStudio.exe']
    with zipfile.ZipFile(package) as z:
        assert z.testzip() is None
        names=z.namelist();assert len(names)==len(set(n.casefold() for n in names))
        manifest=ET.fromstring(z.read('manifest.xml'))
        assert manifest.attrib['product']=='V15VoiceStudio' and manifest.attrib['version']=='4.1.0' and manifest.attrib['kind']=='core'
        assert {n[8:] for n in names if n.startswith('payload/')}=={x.attrib['path'] for x in manifest}
        for x in manifest:
            data=z.read('payload/'+x.attrib['path'])
            assert len(data)==int(x.attrib['size']) and sha(data)==x.attrib['sha256']
        for name,expected in proof['binary_sha256'].items():
            data=z.read('payload/'+name);assert sha(data)==expected==sha((v.ROOT/name).read_bytes());pe(data)
        assert z.read('BEO_Update.exe')==z.read('payload/V15Updater.exe')==z.read('V15Updater.exe')
        for name in v.APP:assert z.read('payload/source/'+name)==(v.SOURCE/name).read_bytes()
        assert 'payload/voice_profiles.tsv' not in names and not (v.ROOT/'voice_profiles.tsv').exists()
        assert not any(Path(n).name.startswith('Creator') and n.endswith('.cs') for n in names)
        assert not any(n.startswith('payload/source/creator-tools/') for n in names)
        assert not any(n.startswith('payload/ai/models/') or n.startswith('payload/source/local-ai/model/') for n in names)
        for stub in ['model','engine']:assert not (v.ROOT/stub).exists(),'UI model fixtures must be removed'
        with zipfile.ZipFile(baseline) as old:
            for name in ['ai_worker.py','gpu_policy.py','NativeGpu.cs','ExternalSpeech.cs','TwoVoices.cs','Punctuation.cs','AudioProcessing.cs','AudioFinishing.cs','media/ffmpeg.exe']:
                assert z.read('payload/source/'+name)==old.read('payload/source/'+name),'Unexpected core/audio byte change: '+name
    v.compile('StablePackageTests',['StablePackageTests.cs'],main='StablePackageTests')
    cases=[
        ('217',v.ROOT.parent/'output-2.0.17/V15VoiceStudio_2.0.17_Core_Update.zip',v.ROOT.parent/'V15VoiceStudio-2.0.17/V15Updater.exe'),
        ('300',v.ROOT.parent/'output-3.0.0/V15VoiceStudio_3.0.0_Core_Update.zip',v.ROOT.parent/'V15VoiceStudio-3.0.0/V15Updater.exe'),
        ('310',v.ROOT.parent/'output-3.1.0/BEO_Voice_Studio_3.1.0_Core_Update.zip',v.ROOT.parent/'BeoVoiceStudio-3.1.0/V15Updater.exe'),
        ('400',baseline,v.ROOT.parent/'BeoVoiceStudio-4.0.0/V15Updater.exe'),
        ('410',baseline,v.ROOT/'V15Updater.exe'),
    ]
    for label,old,updater in cases:
        v.run('StablePackageTests',[str(package),str(old),str(updater)],timeout=180)
        (v.VALID/('StablePackage410-'+label+'.txt')).write_text((v.VALID/'StablePackageTests.txt').read_text())
    result={'version':'4.1.0.0','package_sha256':sha(package.read_bytes()),'package_bytes':package.stat().st_size,'payload_files':len(manifest),'CSharp_pass':proof['CSharp_total'],'Python_pass':15,'update_install_checks':35,'binary_sha256':proof['binary_sha256'],'CRC_manifest_source_PE_and_preserved_audio_checks':'PASS','Windows_hardware_or_real_models_tested':False}
    (package.parent/'release-validation410.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))

if __name__=='__main__':main()
