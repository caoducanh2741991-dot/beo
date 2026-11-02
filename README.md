# BÉO Voice Studio Lite 4.1.2

Bản Lite đọc nguyên lời TXT/SRT, bỏ AI tự sửa và tự dịch theo yêu cầu mới.
Không cần tải/chạy model sửa lời Qwen3-4B (2.497.281.120 byte).

Giữ engine TTS, voice clone, một/hai giọng, SRT khóa/V15.6, hơi thở, nghỉ,
cân âm, batch, cache, phần trăm hoàn thành và ETA.
Mỗi file kết thúc có một voice-report.txt, cả khi lỗi.
WAV/final.srt khớp lời thực sự dùng để TTS. Không tự thay lời hoặc tạo bản dịch giả.

**Máy mới:** đây là bản core. Vẫn cần model/runtime TTS.
Bộ cài nền cũ yêu cầu 15 phần V15VoiceStudio_1.4_Data.7z.001 đến .015;
chưa tìm thấy các phần này trong các tệp hiện truy cập được.
Bỏ model sửa lời 2,5 GB không thay thế dữ liệu giọng.

Build: source/BUILD_WINDOWS.cmd. Kiểm thử: source/validate412lite.py.
Giữ định danh EXE/cache V15VoiceStudio để tương thích.
Kết quả kiểm thử thực tế nằm trong KET_QUA_KIEM_THU_4.1.2.txt và
source/validation-4.1.2-lite sau khi quy trình phát hành hoàn thành.
TTS/GPU dùng fixture trong Linux/Mono; chưa thử Windows/RTX/clone thật.

Các bản AI cũ và 36 snapshot được nhập từ gói gốc vẫn còn trong tag/lịch sử.
archive/ là danh mục gói lưu trữ, không phải danh sách đủ thành phần cài máy mới.

