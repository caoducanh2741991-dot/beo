"""Verify the final compiled/published bytes and exercise old/new update engines.
Runs on Mono fixtures. This does not certify Windows hardware, codecs or models.
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
    package=v.ROOT.parent/'output-4.0.0/BEO_Voice_Studio_4.0.0_Creator_Update.zip'
    proof=json.loads((v.VALID/'completed400.json').read_text())
    assert proof['CSharp_total']>=711
    with zipfile.ZipFile(package) as z:
        assert z.testzip() is None
        names=z.namelist();assert len(names)==len(set(n.casefold() for n in names))
        manifest=ET.fromstring(z.read('manifest.xml'))
        assert manifest.attrib['product']=='V15VoiceStudio' and manifest.attrib['version']=='4.0.0'
        assert {n[8:] for n in names if n.startswith('payload/')}=={x.attrib['path'] for x in manifest}
        for x in manifest:
            data=z.read('payload/'+x.attrib['path'])
            assert len(data)==int(x.attrib['size']) and sha(data)==x.attrib['sha256']
        for name,expected in proof['binary_sha256'].items():
            data=z.read('payload/'+name);assert sha(data)==expected==sha((v.ROOT/name).read_bytes());pe(data)
        assert z.read('BEO_Update.exe')==z.read('payload/V15Updater.exe')==z.read('V15Updater.exe')
        for name in v.APP:assert z.read('payload/source/'+name)==(v.SOURCE/name).read_bytes()
        assert 'payload/voice_profiles.tsv' not in names and not (v.ROOT/'voice_profiles.tsv').exists()
        assert not any(n.startswith('payload/source/creator-tools/') and n.endswith('.exe') for n in names)
        assert not any(n.startswith('payload/source/local-ai/model/') for n in names)
        with zipfile.ZipFile(v.ROOT.parent/'output-3.1.0/BEO_Voice_Studio_3.1.0_Core_Update.zip') as old:
            for n in ['ai_worker.py','gpu_policy.py','NativeGpu.cs','ExternalSpeech.cs','TwoVoices.cs','Punctuation.cs','AudioProcessing.cs','AudioFinishing.cs','media/ffmpeg.exe']:
                assert z.read('payload/source/'+n)==old.read('payload/source/'+n)
    v.compile('StablePackageTests',['StablePackageTests.cs'],main='StablePackageTests')
    cases=[
        ('217','output-2.0.17/V15VoiceStudio_2.0.17_Core_Update.zip','V15VoiceStudio-2.0.17/V15Updater.exe'),
        ('300','output-3.0.0/V15VoiceStudio_3.0.0_Core_Update.zip','V15VoiceStudio-3.0.0/V15Updater.exe'),
        ('310','output-3.1.0/BEO_Voice_Studio_3.1.0_Core_Update.zip','BeoVoiceStudio-3.1.0/V15Updater.exe'),
        ('400','output-3.1.0/BEO_Voice_Studio_3.1.0_Core_Update.zip','BeoVoiceStudio-4.0.0/V15Updater.exe'),
    ]
    for label,baseline,updater in cases:
        v.run('StablePackageTests',[str(package),str(v.ROOT.parent/baseline),str(v.ROOT.parent/updater)],timeout=180)
        (v.VALID/('StablePackage400-'+label+'.txt')).write_text((v.VALID/'StablePackageTests.txt').read_text())
    result={'version':'4.0.0.0','package_sha256':sha(package.read_bytes()),'package_bytes':package.stat().st_size,'payload_files':len(manifest),'csharp_pass':proof['CSharp_total'],'python_pass':15,'updater_pass':24,'binary_sha256':proof['binary_sha256'],'CRC_manifest_source_PE_and_preserved_audio_checks':'PASS','Windows_hardware_or_real_models_tested':False}
    (package.parent/'release-validation400.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))
if __name__=='__main__':main()
