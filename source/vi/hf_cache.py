"""Copy-only Hugging Face cache for the isolated V15 Vietnamese runtime.

The adapter is deliberately pinned to huggingface_hub 0.36.2. It never changes
Windows permissions or the English runtime, and never moves/deletes cached blobs.
"""
import os
from pathlib import Path
import shutil
import tempfile


def comparison_path(path):
    # HF may pass the Windows extended-length spelling for a long snapshot path.
    # Normalize only for containment checks; retain original paths for actual I/O.
    value = os.path.normcase(os.fspath(path))
    if value[:8].lower() == '\\\\?\\unc\\':
        value = '\\\\' + value[8:]
    elif value.startswith('\\\\?\\'):
        value = value[4:]
    return Path(value)


def copy_blob(src, dst, *, new_blob=False, cache_root):
    root = Path(cache_root).resolve()
    source = Path(src).resolve(strict=True)
    destination = Path(os.path.abspath(dst))
    parent = destination.parent.resolve()
    if not comparison_path(source).is_relative_to(comparison_path(root)) or not comparison_path(parent).is_relative_to(comparison_path(root)):
        raise ValueError('Tệp model nằm ngoài bộ nhớ đệm tiếng Việt.')
    if not source.is_file():
        raise ValueError('Tệp model tải đệm không hợp lệ.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    # HF may call this again after a concurrent download. Same-file is already safe.
    if destination.exists() and not destination.is_symlink() and os.path.samefile(source, destination):
        return
    fd, temporary = tempfile.mkstemp(prefix='.v15-copy-', suffix='.tmp', dir=destination.parent)
    temp = Path(temporary)
    try:
        with os.fdopen(fd, 'wb') as output, source.open('rb') as input_file:
            shutil.copyfileobj(input_file, output, 1024 * 1024)
            output.flush()
            os.fsync(output.fileno())
        if temp.stat().st_size != source.stat().st_size:
            raise IOError('Sao chép model chưa đủ dữ liệu: ' + destination.name)
        # Replace only after the complete copy; leave the blob available for resume.
        os.replace(temp, destination)
    finally:
        temp.unlink(missing_ok=True)


def configure_copy_cache(hf_home):
    import huggingface_hub
    import huggingface_hub.file_download as download
    if huggingface_hub.__version__ != '0.36.2':
        raise RuntimeError('Bộ tải tiếng Việt cần huggingface_hub 0.36.2. Chạy lại Cài gói Việt để sửa thư viện.')
    cache_root = Path(hf_home).resolve() / 'hub'
    cache_root.mkdir(parents=True, exist_ok=True)
    # Do not merely hide the Windows warning: prevent symlink creation/probing.
    download.are_symlinks_supported = lambda cache_dir=None: False
    def copy_only(src, dst, new_blob=False):
        return copy_blob(src, dst, new_blob=new_blob, cache_root=cache_root)
    download._create_symlink = copy_only
