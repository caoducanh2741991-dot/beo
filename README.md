# BÉO Voice Studio

Ứng dụng Windows offline tạo voice từ TXT/SRT. Mốc mã nguồn hiện tại: **4.1.1 — Render Fix**.
Tên hiển thị là BÉO Voice Studio; executable, cache và định danh cập nhật vẫn dùng `V15VoiceStudio` để tương thích.

## Nội dung

- `source/`: mã C# WinForms, worker Python, script build/đóng gói, fixture, kiểm thử và giấy phép phụ thuộc.
- `V15VoiceStudio.exe`, `V15Updater.exe`: binary gốc đã build của 4.1.1, lấy từ gói cập nhật; không build lại trong đợt lưu trữ.
- `ai/`: worker và chính sách GPU trong gói.
- `release-info/`: manifest gốc, hướng dẫn, báo cáo nâng cấp/kiểm thử và ảnh giao diện của phiên bản hiện tại.
- `docs/`: tài liệu bàn giao cho Gemini/lập trình viên tiếp theo và dữ liệu VAN để đối chiếu lỗi.
- `archive/`: danh mục, SHA-256 và ánh xạ các bản cập nhật sang commit/tag.
- `source/validation-4.1.1/`: kết quả kiểm tra đã có trong gói phát hành, giữ nguyên.

Lịch sử Git được **nhập từ các gói cũ**, không phải lịch sử commit phát triển ban đầu.
Các phiên bản có mã nguồn trọn vẹn trong gói được gắn tag tương ứng; xem [danh mục](archive/README.md).
Các ZIP, bộ cài, bản vá, checkpoint và gói model chia nhỏ được giữ nguyên để tải lên GitHub Releases.

## Build trên Windows

1. Cài .NET Framework 4.5 hoặc môi trường tương thích có `Framework64/v4.0.30319/csc.exe`.
2. Trong thư mục `source`, chạy `BUILD_WINDOWS.cmd`.
3. Script tạo `V15VoiceStudio.exe` và `V15Updater.exe` ở thư mục cha; app build x64.
4. Dùng model, runtime và giọng đã cài theo hướng dẫn của ứng dụng/gói cập nhật.

Gói core và mã nguồn không chứa đủ mọi engine/model giọng để chạy TTS ngay trên máy trắng.
Các gói thành phần offline cũ được lưu riêng; không suy đoán rằng chúng bao phủ mọi giọng hoặc clone trên máy người dùng.
Khi thêm tệp C#, cập nhật cả danh sách trong `BUILD_WINDOWS.cmd` và `run_validation.py`.

## Kiểm thử và giới hạn bằng chứng

Đọc `release-info/KET_QUA_KIEM_THU_4.1.1.txt` và [tài liệu bàn giao](docs/BAN_GIAO_CHO_GEMINI.txt).
Bộ kiểm tra hiện có gồm `validate411.py`, `benchmark411.py`, `verify_release411.py` và các test hồi quy cũ.
Môi trường Linux/Mono cần đặt `V15_BUILD_TOOLS` và `V15_NET45_REFS` theo toolchain thực.
Một số script kiểm tra updater/benchmark có đường dẫn tới phiên bản nền; phải cung cấp đúng gói và điều chỉnh đường dẫn trước khi chạy.

Báo cáo 4.1.1 ghi nhận 745 kiểm tra C#, 15 Python và 28 kiểm tra cập nhật trên Mono/Linux,
WinForms/Xvfb và FFmpeg. Speech/AI dùng fixture hoặc replay có nguồn.
**Chưa xác nhận trên Windows vật lý, RTX 2070 hoặc model giọng/clone thật của người dùng.**
Đợt lưu trữ này kiểm tra CRC/hash và đối chiếu byte mã nguồn, không được diễn giải thành kiểm thử TTS/GPU mới.

## Quy tắc phát triển phải giữ

- Hai chế độ: theo file không tự sửa (một báo cáo/file), hoặc AI tự sửa hoàn toàn tự động (hai báo cáo/file, gồm đối chiếu và các câu đổi Anh–Việt).
- WAV và `final.srt` khớp lời thật đưa vào TTS; giữ nguyên tệp nguồn. Bản dịch thiếu phải báo `TRANSLATION_INCOMPLETE`.
- AI sửa SRT khóa/V15.6, giữ nhân vật, số liệu, hành động, sự kiện và quan hệ nhân quả. Tối đa ba chiến lược khác nhau và đo lại đúng giọng.
- Lỗi sửa chuyển sang trạng thái cần kiểm tra và giữ tiến độ các chặng đạt; không che lỗi bằng việc WAV tồn tại.
- Giữ giọng đơn/đôi, voice clone, engine cũ, cache, hơi thở, nghỉ và cân âm.
- Giới hạn kỹ thuật 0,90–1,25x; ưu tiên 0,90–1,10x. Không đổi tốc độ đọc hoặc chèn im lặng để tuyên bố tăng tốc xử lý.
- Không tự đưa trang Creator/đóng gói clip đã gỡ trở lại; chức năng lấy SRT từ video cũ vẫn giữ.

## Tiếp tục bằng Gemini hoặc công cụ khác

Bắt đầu từ `docs/BAN_GIAO_CHO_GEMINI.txt`, đọc mã nguồn và báo cáo liên quan trước khi sửa.
Mỗi lần bàn giao ghi rõ phiên bản nền, tệp gây lỗi, giọng/engine/cấu hình, trạng thái cache,
phép đo đã chạy, kết quả và phần chưa kiểm chứng.

Giấy phép của các thành phần bên thứ ba nằm cạnh mã/tài nguyên tương ứng. Không gán giấy phép nguồn mở mới cho toàn bộ ứng dụng trong đợt lưu trữ này.
