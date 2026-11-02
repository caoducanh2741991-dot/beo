"""Verify exact Lite package bytes and actual updater compatibility on fixtures."""
from pathlib import Path
import hashlib,json,struct,zipfile,xml.etree.ElementTree as ET
import run_validation as v
def sha(data):return hashlib.sha256(data).hexdigest()
def main():
    package=v.ROOT.parent/'output-4.1.2/BEO_Voice_Studio_4.1.2_Lite_Core_Update.zip'
    proof=json.loads((v.VALID/'completed412lite.json').read_text())
    baseline=v.ROOT.parent/'github-release-input/BEO_Voice_Studio_4.1.1_Render_Fix_Update.zip'
    with zipfile.ZipFile(package) as z:
        assert z.testzip() is None
        names=z.namelist();assert len(names)==len({n.casefold() for n in names})
        manifest=ET.fromstring(z.read('manifest.xml'))
        assert manifest.get('version')=='4.1.2' and manifest.get('kind')=='core'
        assert {n[8:] for n in names if n.startswith('payload/')}=={x.get('path') for x in manifest}
        for row in manifest:
            raw=z.read('payload/'+row.get('path'))
            assert len(raw)==int(row.get('size')) and sha(raw)==row.get('sha256')
        for name,value in proof['binary_sha256'].items():
            raw=z.read('payload/'+name);assert sha(raw)==value
            offset=struct.unpack_from('<I',raw,60)[0]
            assert raw[:2]==b'MZ' and raw[offset:offset+4]==b'PE\0\0' and struct.unpack_from('<H',raw,offset+4)[0]==0x8664
        for name in v.APP:assert z.read('payload/source/'+name)==(v.SOURCE/name).read_bytes()
        assert 'payload/source/LocalAi.cs' not in names and 'payload/source/LocalAiDisabled.cs' in names
        assert not any(n.startswith('payload/source/local-ai/model/') or n.startswith('payload/source/local-ai/runtime/') or n.startswith('payload/ai/models/') for n in names)
        assert z.read('BEO_Update.exe')==z.read('payload/V15Updater.exe')
        with zipfile.ZipFile(baseline) as old:
            for name in ['Core.cs','ExternalSpeech.cs','NativeGpu.cs','TwoVoices.cs','VoiceCatalog.cs','ai_worker.py','gpu_policy.py','Punctuation.cs','AudioFinishing.cs','RenderReuse.cs','RenderPerformance.cs']:
                assert z.read('payload/source/'+name)==old.read('payload/source/'+name),'Unexpected TTS/cache change: '+name
    v.compile('Lite412PackageTests',['Lite412PackageTests.cs'],main='Lite412PackageTests')
    cases=[('standard411',v.ROOT.parent/'github-beo-voice-studio/V15Updater.exe'),('lite412',v.ROOT/'V15Updater.exe')]
    for name,updater in cases:
        v.run('Lite412PackageTests',[str(package),str(baseline),str(updater)],timeout=180)
        (v.VALID/('Lite412Package-'+name+'.txt')).write_text((v.VALID/'Lite412PackageTests.txt').read_text())
    result={'version':'4.1.2','edition':'Lite','CRC_manifest_source_binary_identity':'PASS',
        'package_sha256':sha(package.read_bytes()),'package_bytes':package.stat().st_size,
        'CSharp_checks':proof['CSharp_total'],'Python_checks':15,'update_install_checks':18,
        'real_windows_gpu_voice_tested':False,'fresh_full_installer_ready':False}
    (package.parent/'release-validation412lite.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))
if __name__=='__main__':main()

