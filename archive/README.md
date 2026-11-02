# Danh mục các gói và snapshot

Các commit và tag được nhập từ gói đã phát hành trong lần lưu trữ này. Không giả lập ngày hoặc tác giả phát triển gốc.
`release-assets.json` và `SHA256SUMS.txt` chứa tên, kích thước và hash của toàn bộ 55 tệp nhị phân gốc.
ZIP cập nhật, installer và các phần chia nhỏ phải được tải lên GitHub Releases; không đưa model hàng GB vào lịch sử Git.

`V15VoiceStudio_1.4.9_WorkCheckpoint.zip` hiện không có cấu trúc ZIP hoàn chỉnh. Giữ nguyên byte để lưu trữ;
không dùng nó làm mã nguồn chuẩn. Bản `V15VoiceStudio_1.4.9_Update.zip` mở được bình thường.

Hai phần của bản 2.0.10 và sáu phần AI offline 2.0.13 được giữ nguyên tên, có thứ tự `.001`, `.002`, ...
Nối đủ các phần theo thứ tự để lấy ZIP; kiểm tra SHA-256 trước khi cài.
Gói AI offline là thành phần phụ của 2.0.13, không tạo một phiên bản mã nguồn khác.
Các installer 1.0.1, 1.1.0, 1.2.0, 1.3.0 và bộ cài nền được giữ dưới dạng tệp gốc; chưa trích xuất thành lịch sử nguồn riêng.
Checkpoint, bản vá 1.4.7 và kiểm tra bổ sung 1.4.11 được giữ trong tài sản phát hành gốc, không gán thành bản nâng cấp đầy đủ.

| Phiên bản | Tag | Gói gốc |
| --- | --- | --- |
| 1.4.1 | `v1.4.1` | `V15VoiceStudio_1.4.1_Update.zip` |
| 1.4.2 | `v1.4.2` | `V15VoiceStudio_1.4.2_Update.zip` |
| 1.4.3 | `v1.4.3` | `V15VoiceStudio_1.4.3_Update.zip` |
| 1.4.3.1 | `v1.4.3.1` | `V15VoiceStudio_1.4.3.1_Update.zip` |
| 1.4.4 | `v1.4.4` | `V15VoiceStudio_1.4.4_Update.zip` |
| 1.4.5 | `v1.4.5` | `V15VoiceStudio_1.4.5_Update.zip` |
| 1.4.6 | `v1.4.6` | `V15VoiceStudio_1.4.6_Update.zip` |
| 1.4.7 | `v1.4.7` | `V15VoiceStudio_1.4.7_Update.zip` |
| 1.4.8 | `v1.4.8` | `V15VoiceStudio_1.4.8_Update.zip` |
| 1.4.9 | `v1.4.9` | `V15VoiceStudio_1.4.9_Update.zip` |
| 1.4.10 | `v1.4.10` | `V15VoiceStudio_1.4.10_Update.zip` |
| 1.4.11 | `v1.4.11` | `V15VoiceStudio_1.4.11_Update.zip` |
| 1.4.12 | `v1.4.12` | `V15VoiceStudio_1.4.12_Update.zip` |
| 2.0.0 | `v2.0.0` | `V15VoiceStudio_2.0_Update.zip` |
| 2.0.1 | `v2.0.1` | `V15VoiceStudio_2.0.1_Update.zip` |
| 2.0.2 | `v2.0.2` | `V15VoiceStudio_2.0.2_Update.zip` |
| 2.0.3 | `v2.0.3` | `V15VoiceStudio_2.0.3_Update.zip` |
| 2.0.4 | `v2.0.4` | `V15VoiceStudio_2.0.4_Update.zip` |
| 2.0.5 | `v2.0.5` | `V15VoiceStudio_2.0.5_Update.zip` |
| 2.0.6 | `v2.0.6` | `V15VoiceStudio_2.0.6_Update.zip` |
| 2.0.7 | `v2.0.7` | `V15VoiceStudio_2.0.7_Update.zip` |
| 2.0.8 | `v2.0.8` | `V15VoiceStudio_2.0.8_Update.zip` |
| 2.0.9 | `v2.0.9` | `V15VoiceStudio_2.0.9_Update.zip` |
| 2.0.10 | `v2.0.10` | `V15VoiceStudio_2.0.10_Update.zip.001` + `V15VoiceStudio_2.0.10_Update.zip.002` |
| 2.0.11 | `v2.0.11` | `V15VoiceStudio_2.0.11_Core_Update.zip` |
| 2.0.12 | `v2.0.12` | `V15VoiceStudio_2.0.12_Core_Update.zip` |
| 2.0.13 | `v2.0.13` | `V15VoiceStudio_2.0.13_Core_Update.zip` |
| 2.0.14 | `v2.0.14` | `V15VoiceStudio_2.0.14_Core_Update.zip` |
| 2.0.15 | `v2.0.15` | `V15VoiceStudio_2.0.15_Core_Update.zip` |
| 2.0.16 | `v2.0.16` | `V15VoiceStudio_2.0.16_Core_Update.zip` |
| 2.0.17 | `v2.0.17` | `V15VoiceStudio_2.0.17_Core_Update.zip` |
| 3.0.0 | `v3.0.0` | `V15VoiceStudio_3.0.0_Core_Update.zip` |
| 3.1.0 | `v3.1.0` | `BEO_Voice_Studio_3.1.0_Core_Update.zip` |
| 4.0.0 | `v4.0.0` | `BEO_Voice_Studio_4.0.0_Creator_Update.zip` |
| 4.1.0 | `v4.1.0` | `BEO_Voice_Studio_4.1.0_Fast_Voice_Update.zip` |
| 4.1.1 | `v4.1.1` | `BEO_Voice_Studio_4.1.1_Render_Fix_Update.zip` |
