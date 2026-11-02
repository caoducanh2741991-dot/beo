"""Verify release hashes, old/new updater compatibility and data preservation."""
from pathlib import Path
import hashlib,shutil,subprocess,tempfile,zipfile,xml.etree.ElementTree as ET
import run_validation as v
root=v.ROOT
base=root.parent/'baseline212/installed'
package=root.parent/'output-2.0.13/V15VoiceStudio_2.0.13_Core_Update.zip'
with zipfile.ZipFile(package) as z:
    assert z.testzip() is None
    assert len(z.namelist())==len(set(z.namelist()))
    manifest=ET.fromstring(z.read('manifest.xml'))
    assert manifest.get('version')=='2.0.13'
    paths=[x.get('path') for x in manifest]
    for item in manifest:
        name=item.get('path');data=z.read('payload/'+name)
        assert len(data)==int(item.get('size'))
        assert hashlib.sha256(data).hexdigest()==item.get('sha256'),name
        assert '/weights/' not in name and '/deps/' not in name
        assert name not in ['voice_profiles.tsv','voices.bin']
        assert not name.startswith(('source/local-ai/model/','source/local-ai/runtime/','source/local-ai/downloads/'))
        assert not any(part.startswith('.') for part in name.split('/'))
    for n in ['LocalAi.cs','AiTimeline.cs','AiTimelineControls.cs','TwoVoices.cs']:
        assert 'source/'+n in paths
    code=z.read('payload/source/LocalAi.cs')
    assert b' --offline ' in code and b'--no-conversation' in code
    assert b'configs.config' not in z.read('payload/source/community/rvc/infer/rmvpe.py')
    assert b'RVC runtime 2.0.11' in z.read('payload/source/community/rvc_worker.py')
    assert package.stat().st_size<50*1024*1024
    extracted=Path(tempfile.mkdtemp(prefix='v15-213-package-'));z.extractall(extracted)
print('PASS core ZIP CRC / every payload hash / version / no voice or AI model replacement',flush=True)
keep=['ai/runtime/python.exe','ai/models/weights.bin','ai/gpu/site-packages/torch/lib/torch.dll','engine/gpu/onnxruntime.dll','model/model.onnx','breaths/custom.wav','libritts/voice.bin','kitten/model.onnx','piper_john/model.onnx','piper_kristin/model.onnx','voice_profiles.tsv','voices.bin','user_voices/voices.xml','settings.xml','example.srt','example.wav',*['source/community/rvc/weights/'+n for n in ['adam.pth','adam.index','hubert.bin','hubert-config.json','rmvpe.pt']],'source/community/rvc/deps/cp311/faiss/__init__.py','source/community/rvc/deps/cp312/faiss/__init__.py','source/local-ai/model/qwen3-4b.gguf','source/local-ai/runtime/cpu/llama-completion.exe','source/local-ai/runtime/gpu/ggml-vulkan.dll','source/local-ai/downloads/part.zip']
for updater in [base/'V15Updater.exe',root/'V15Updater.exe']:
    for pack in [package,extracted]:
        target=Path(tempfile.mkdtemp(prefix='v15-213-installed-'))
        for name in ['V15VoiceStudio.exe','V15Updater.exe']:shutil.copyfile(base/name,target/name)
        for name in keep:
            p=target/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(('KEEP '+name).encode())
        before={name:hashlib.sha256((target/name).read_bytes()).hexdigest() for name in keep}
        result=subprocess.run([v.MONO,str(updater),'--apply-cli','--zip' if pack==package else '--folder',str(pack),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=90)
        assert result.returncode==0,result.stdout+result.stderr
        for name in ['V15VoiceStudio.exe','V15Updater.exe']:assert (target/name).read_bytes()==(root/name).read_bytes()
        for name,digest in before.items():assert hashlib.sha256((target/name).read_bytes()).hexdigest()==digest,name
        print('PASS',updater.parent.name,'ZIP' if pack==package else 'folder','upgrade; preserves',len(keep),'data paths',flush=True)
        old=root.parent/'baseline212/V15VoiceStudio_2.0.12_Core_Update.zip'
        result=subprocess.run([v.MONO,str(root/'V15Updater.exe'),'--apply-cli','--zip',str(old),'--target',str(target)],env=v.ENV,capture_output=True,text=True,timeout=90)
        assert result.returncode!=0
        assert (target/'V15VoiceStudio.exe').read_bytes()==(root/'V15VoiceStudio.exe').read_bytes()
        print('PASS downgrade rejected and current application preserved',flush=True)
        shutil.rmtree(target)
shutil.rmtree(extracted)
print('RELEASE CORE 2.0.13 VERIFIED (Mono/Linux; Windows/physical GPU not tested)')
