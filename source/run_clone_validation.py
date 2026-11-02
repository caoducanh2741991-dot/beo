"""Reproduce clone-policy, worker-protocol and process-lifecycle regressions.

Run after run_validation.py. Requires Mono/net45 refs as configured there,
FFmpeg on PATH, and --python pointing to Python with numpy and soundfile.
These failure-injection tests do not need or download model weights.
"""
from pathlib import Path
import argparse,importlib.util,os,shutil,subprocess,sys,tempfile

SOURCE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('validation',SOURCE/'run_validation.py')
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--python',default=sys.executable)
    args=parser.parse_args()
    for name in ['test_clone_generation.py','test_clone_worker_protocol.py']:
        r=subprocess.run([args.python,str(SOURCE/name)],capture_output=True,text=True,timeout=100)
        (v.VALID/(name+'.txt')).write_text(r.stdout+r.stderr)
        if r.returncode:raise RuntimeError(r.stdout+r.stderr)
        print(name,'PASS',flush=True)
    with tempfile.TemporaryDirectory(prefix='v15-clone-regression-') as temporary:
        root=Path(temporary)
        for name in ['ai/ffmpeg.exe','ai/reference.wav','ai/models/qwen_base/config.json',
                     'ai/models/qwen_base/model.safetensors',
                     'ai/models/qwen_custom/speech_tokenizer/model.safetensors']:
            path=root/name;path.parent.mkdir(parents=True,exist_ok=True);path.touch()
        shutil.copyfile(SOURCE/'clone_worker_fixture.py',root/'ai/worker.py')
        v.compile('ExternalCloneTests',['ExternalCloneTests.cs'],main='ExternalCloneTests',app=True)
        v.run('ExternalCloneTests',[str(root)],env=dict(v.ENV,V15_PYTHON=args.python),timeout=100)
    print('CLONE REGRESSION COMPLETE (failure injection; not physical GPU/Windows testing)')

if __name__=='__main__':main()
