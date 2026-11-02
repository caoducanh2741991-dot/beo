"""Check imports in the isolated Vietnamese runtime, including resumed installs."""
import argparse
import importlib
import importlib.metadata
import json

CORE = [
    ('numpy', 'numpy==2.1.3'), ('soundfile', 'soundfile==0.14.0'),
    ('soxr', 'soxr==1.1.0'), ('onnxruntime', 'onnxruntime==1.23.2'),
    ('kaldi_native_fbank', 'kaldi-native-fbank==1.22.3'),
    ('tokenizers', 'tokenizers==0.22.2'), ('huggingface_hub', 'huggingface_hub==0.36.2'),
    ('sea_g2p', 'sea-g2p==0.9.1'), ('librosa', 'librosa==0.11.0'),
    ('yaml', 'PyYAML==6.0.3'), ('vieneu', 'vieneu==3.8.3'),
]
CUDA = [('torch', 'torch==2.8.0'), ('torchaudio', 'torchaudio==2.8.0'),
        ('transformers', 'transformers==4.57.6'), ('safetensors', 'safetensors==0.6.2'),
        ('einops', 'einops==0.8.1')]


def inspect(mode):
    errors = []
    for module, spec in CORE + (CUDA if mode == 'gpu' else []):
        distribution, expected = spec.split('==')
        try:
            installed = importlib.metadata.version(distribution)
            if installed.split('+')[0] != expected:
                raise RuntimeError('Phiên bản ' + installed + ', cần ' + expected)
            loaded = importlib.import_module(module)
            if module == 'torch' and mode == 'gpu' and loaded.version.cuda != '12.8':
                raise RuntimeError('Cần wheel CUDA 12.8 trong gói tiếng Việt riêng.')
        except Exception as error:
            errors.append({'spec': spec, 'error': str(error)[:700]})
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--mode', choices=['gpu', 'cpu'], required=True)
    args = parser.parse_args()
    print('V15_DEPS\t' + json.dumps(inspect(args.mode), ensure_ascii=True), flush=True)
