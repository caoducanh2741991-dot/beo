# BÉO Voice Studio Lite 4.1.2

Yêu cầu mới nhất: bỏ AI tự sửa để nhẹ hơn. Bản Lite chỉ đọc nguyên file; không khôi phục chế độ tự viết lại/dịch nếu chưa có yêu cầu mới.

- C# WinForms, .NET Framework 4.5, x64. Giữ tên EXE/product/cache V15VoiceStudio để tương thích.
- source/BUILD_WINDOWS.cmd build Windows; source/validate412lite.py build và kiểm thử trên Mono/Linux. source/run_validation.py chuyển đến bộ kiểm thử Lite.
- LocalAiDisabled.cs thay LocalAi.cs; các lệnh cài/chạy/download LLM bị chặn. Không phát hành lại LocalAi.cs hoặc package_ai_offline.py.
- WorkflowRunner/WorkflowReports ép chế độ theo file và đúng một voice-report.txt khi thành công hoặc lỗi. Không gọi prepare/translate từ cấu hình AI cũ.
- ProjectState: thiết lập cũ về workflow=1/AI=0. Job AI cũ xếp lại từ nguồn; job theo file đã đạt giữ tiến độ hợp lệ. Không xóa WAV/model người dùng.
- Giữ TTS, một/hai giọng, clone, audio/cache, timeline, hơi thở/ngắt nghỉ/cân âm, % và ETA. Qwen3-TTS khác model sửa lời Qwen3-4B.
- source/publish412lite.py chỉ đóng gói tại chỗ. CI chỉ chạy thủ công, contents: read; không tự đẩy code hoặc phát hành.

Đợt build đã đạt 438 kiểm tra C#, 15 Python và 18 kiểm tra cài bằng updater 4.1.1/4.1.2. Bằng chứng: source/validation-4.1.2-lite và archive/lite412-build-provenance.json. TTS/GPU là fixture; chưa xác nhận Windows vật lý, RTX 2070, model/clone thật hoặc tốc độ TTS thực.

Gói core không cài độc lập trên máy trắng. Thiếu dữ liệu nền 15 phần V15VoiceStudio_1.4_Data.7z.001–.015. Cần phục hồi dữ liệu/runtime TTS hoặc thư mục app đầy đủ để đóng gói full. Bỏ Qwen3-4B khoảng 2,5 GB không thay thế các model tạo giọng. Updater không tự xóa model LLM cũ.

Giữ các bản trước trong Git tags và Releases; không ghi đè gói cũ. Trước khi phát hành tiếp, build và chạy kiểm thử thực tương ứng, lưu giới hạn kiểm thử rõ ràng.

## Bổ sung bản cài mới ngày 03/11/2026

Đã tái tạo gói Fresh Kokoro từ model/bộ chạy chính thức, kèm 20 giọng US (Adam SID 11), bộ phát âm, Windows x64/VC runtime cục bộ và FFmpeg. Không cần dữ liệu nền 15 phần để dùng Kokoro. Đây là bản CPU; không gói Chatterbox/Qwen3-TTS/clone/CUDA và các model tùy chọn khác.

Đã kiểm thử trên Windows Server 2025: 13 kiểm tra giọng thật/UI staging + 13 sau cài, 21 hồi quy updater + 5 kiểm tra nền Kokoro/Python. Đã cài vào thư mục trống, đối chiếu 804 tệp, cập nhật Universal Core trên bản Fresh, giữ model và tệp riêng khi gỡ cài đặt. Đã kiểm tra thêm 5 trường hợp FFmpeg Windows (atempo 0,90x/1,10x, tín hiệu audio, loudnorm). Chưa chạy RTX 2070 hoặc clone thật.

Updater mới cho phép nền Python cũ hoặc nền Kokoro đủ tệp; không tạo python.exe/model giả để qua kiểm tra. App EXE giữ nguyên edf76a5dc7fcf080af66c77b84ff82dd5a259d6a10a6dcc2eb89e00ace6ece53. Updater đã build Windows: a02594c8e91bf58f657a62678f360d05687e90c10813733d829d0e353be1d8d9.

Dùng source/fresh_install412.py và .github/workflows/fresh412.yml để tái tạo. Bằng chứng mới ở source/validation-4.1.2-windows và archive/lite412-fresh-build-provenance.json. Gói Universal Core mới ưu tiên hơn gói Lite Core ban đầu. Xem README để chọn đúng gói.
