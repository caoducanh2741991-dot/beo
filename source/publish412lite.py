"""Build an honest core release and preserve the matching verification evidence."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parent.parent
def main():
    proof=json.loads((ROOT/'validation/completed412lite.json').read_text())
    report=ROOT/'KET_QUA_KIEM_THU_4.1.2.txt'
    def write_report(installs):
        report.write_text('BÉO VOICE STUDIO LITE 4.1.2 — KIỂM THỬ\n\n'
            +f"C# PASS: {proof['CSharp_total']}. Python PASS: 15. Kiểm tra cài cập nhật: {installs}.\n"
            +'Mono/Linux, WinForms/Xvfb và FFmpeg/process thật; TTS/GPU là fixture.\n'
            +'Không có thử nghiệm Windows vật lý, RTX 2070 hoặc clone/model thật.\n'
            +'Đọc nguyên file; không gọi sửa/dịch; một báo cáo cả thành công/lỗi.\n'
            +'SRT/voice trùng lời; giữ timeline; cache warm không gọi lại TTS và WAV giữ byte.\n'
            +'Job AI cũ chuyển về nguồn; job theo file đã đạt giữ tiến độ/cache.\n'
            +'UI không còn nút AI tự sửa hoặc cài LLM; giữ clone, engine, một/hai giọng.\n'
            +'GÓI CORE — CHƯA CÓ BỘ CÀI FULL MÁY TRẮNG. Thiếu 15 phần dữ liệu nền.\n\n'
            +'\n'.join(k+' SHA256: '+v for k,v in proof['binary_sha256'].items())+'\n')
    (ROOT/'BAO_CAO_NANG_CAP_4.1.2.txt').write_text(
        'LITE 4.1.2: bỏ AI tự sửa/dịch và downloader/runner LLM khỏi build.\n'
        'Không cần model sửa lời 2.497.281.120 byte; giữ model/engine tạo giọng riêng.\n'
        'Một workflow, một báo cáo/file. Giữ clone, một/hai giọng, cache, tiến độ và ETA.\n'
        'Máy mới vẫn cần dữ liệu/runtime TTS; chưa tìm thấy bộ dữ liệu nền 15 phần.\n')
    write_report('chưa kết thúc phép kiểm tra gói')
    subprocess.run([sys.executable,str(ROOT/'source/package_release.py')],check=True)
    subprocess.run([sys.executable,str(ROOT/'source/verify_release412lite.py')],check=True)
    write_report(18)
    subprocess.run([sys.executable,str(ROOT/'source/package_release.py')],check=True)
    subprocess.run([sys.executable,str(ROOT/'source/verify_release412lite.py')],check=True)
    evidence=ROOT/'source/validation-4.1.2-lite';evidence.mkdir(exist_ok=True)
    for p in (ROOT/'validation').iterdir():
        if p.is_file() and p.suffix in ('.txt','.json','.png'):shutil.copyfile(p,evidence/p.name)
    out=ROOT.parent/'output-4.1.2'
    shutil.copyfile(out/'release-validation412lite.json',ROOT/'archive/lite412-release-validation.json')
    assets=[out/'BEO_Voice_Studio_4.1.2_Lite_Core_Update.zip',report,ROOT/'HUONG_DAN_4.1.2.txt',out/'release-validation412lite.json']
    for p in assets[1:]:
        if p.parent!=out:shutil.copyfile(p,out/p.name)
    (out/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in assets))
    (ROOT/'ReleaseNotes412lite.md').write_text(
        '# BÉO Voice Studio Lite 4.1.2\n\n'
        'Đọc nguyên lời TXT/SRT; bỏ AI tự sửa và tự dịch. Không cần gói model sửa lời Qwen3-4B khoảng 2,5 GB.\n\n'
        'Giữ engine TTS, clone, một/hai giọng, cache, timeline, tiến độ và ETA. Mỗi file có một báo cáo.\n\n'
        +f"Đã build x64 và đạt {proof['CSharp_total']} kiểm tra C#, 15 Python, 18 kiểm tra cập nhật. "
        'Linux/Mono + WinForms/Xvfb + FFmpeg thật; TTS/GPU fixture. Chưa thử Windows vật lý, RTX 2070 hoặc clone thật.\n\n'
        '**Đây là gói core, chưa phải bộ cài full cho máy mới.** Còn thiếu 15 phần dữ liệu nền 1.4. '
        'Đọc HUONG_DAN_4.1.2.txt trước khi dùng.\n')
if __name__=='__main__':main()

