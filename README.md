# BÉO Voice Studio Lite 4.1.2

**Tải ứng dụng tại [Releases · v4.1.2](https://github.com/caoducanh2741991-dot/beo/releases/tag/v4.1.2).**

| Máy đang dùng | Tệp cần tải | Cách dùng |
|---|---|---|
| Máy Windows mới | `BEO_Voice_Studio_4.1.2_Fresh_Kokoro_Setup.exe` | Chạy bộ cài, chọn thư mục mới, mở biểu tượng BÉO Voice Studio Lite. |
| Muốn dùng bản portable | `BEO_Voice_Studio_4.1.2_Fresh_Kokoro_Portable.zip` | Giải nén toàn bộ, chạy `BAT_DAU.cmd`. |
| Đã có ứng dụng và bộ giọng | `BEO_Voice_Studio_4.1.2_Lite_Universal_Core_Update.zip` | Giải nén, đóng app, chạy `BEO_Update.exe`, chọn đúng thư mục đang cài. |

`Code → Download ZIP` tải mã nguồn; hãy dùng các tệp ứng dụng trong Releases.

Bản Fresh có sẵn model Kokoro v1.0, 20 giọng tiếng Anh Mỹ, bộ phát âm,
runtime Windows x64 và FFmpeg. Chọn **Kokoro · Adam** để dùng Adam (SID 11).
Không cần 15 phần dữ liệu nền cũ để sử dụng Kokoro trong gói này.

Gói Fresh chạy TTS trên CPU. Dữ liệu Chatterbox, Qwen3-TTS/clone, các engine
khác và CUDA không gói kèm. Các chức năng này vẫn có trong mã ứng dụng;
gói Universal Core giữ các model/runtime/giọng riêng đã cài trên máy cũ.

Lite đọc nguyên lời TXT/SRT, bỏ AI tự sửa và tự dịch; không cần model sửa lời
Qwen3-4B khoảng 2,5 GB. Giữ một/hai giọng, timeline, hơi thở, nghỉ, cân âm,
batch, cache, % hoàn thành và ETA. Mỗi file kết thúc có đúng một voice-report.txt.

## Kiểm thử

- App EXE giữ nguyên bản Lite đã đạt 438 C#, 15 Python, 18 kiểm tra cập nhật
  trên Mono/Linux (speech/GPU fixture).
- [Build Windows đã đạt](https://github.com/caoducanh2741991-dot/beo/actions/runs/41587951214):
  model Kokoro thật, 13 kiểm tra audio/UI trên bản staging và 13 kiểm tra trên
  bản cài mới; 21 hồi quy updater, 5 kiểm tra nền Kokoro/Python; kiểm tra 804 tệp
  cài đặt, cập nhật core trên bản Fresh và giữ tệp riêng khi gỡ cài đặt.
- Thêm 5 kiểm tra FFmpeg Windows: đổi tốc độ 0,90x/1,10x, tín hiệu audio và đo loudnorm.
- Windows build chạy trên Windows Server 2025 x64. Chưa thử RTX 2070 hoặc model clone thật.
- Đối chiếu `SHA256SUMS_FRESH.txt`, `FRESH_INSTALL_VALIDATION.json` và báo cáo kèm gói.

## Tiếp tục phát triển

`source/BUILD_WINDOWS.cmd` build ứng dụng. `source/fresh_install412.py` đóng gói
và kiểm tra bản máy mới. CI chạy thủ công với quyền đọc, không tự đẩy mã hoặc
phát hành. Mã nguồn, giấy phép, kiểm thử và bằng chứng được lưu trong repo.
Các gói và phiên bản cũ còn trong tags/Releases. Giữ định danh EXE/cache
`V15VoiceStudio` để tương thích với dữ liệu cũ.
