using System;using System.IO;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
public static class MediaSupport {
 static readonly HashSet<string> checkedPaths=new HashSet<string>();
 public static void Ensure(string root,Func<bool> cancel){string exe=Tempo.Executable(root),key=exe+(File.Exists(exe)?new FileInfo(exe).LastWriteTimeUtc.Ticks.ToString():"");lock(checkedPaths){if(checkedPaths.Contains(key))return;}
  string enc=AudioProcess.Run(root,"-v error -encoders",cancel),filters=AudioProcess.Run(root,"-v error -filters",cancel),formats=AudioProcess.Run(root,"-v error -demuxers",cancel);
  foreach(string required in new[]{"pcm_s16le","pcm_f32le"})if(!Regex.IsMatch(enc,@"\b"+required+@"\b"))throw new Exception("Bộ xử lý âm thanh thiếu "+required+". Giải nén đầy đủ ZIP 1.4.6 hoặc cập nhật từ ZIP; không lấy riêng EXE.");
  foreach(string required in new[]{"loudnorm","atempo","aresample","apad","atrim","asetpts","volume"})if(!Regex.IsMatch(filters,@"\b"+required+@"\b"))throw new Exception("Bộ xử lý âm thanh thiếu bộ lọc "+required+". Cài đủ ZIP cập nhật 1.4.6.");
  if(!formats.Contains("mov")||!formats.Contains("mp3")||!formats.Contains("wav"))throw new Exception("Bộ xử lý âm thanh chưa hỗ trợ đủ MP4/MP3/WAV. Cài đủ ZIP cập nhật 1.4.6.");lock(checkedPaths)checkedPaths.Add(key);
 }
 public static Exception InputError(string path,Exception e){if(e is OperationCanceledException||e is TimeoutException)return e;return new IOException("Chưa đọc được âm thanh từ "+Path.GetFileName(path)+". Kiểm tra file đã tải xong, có track âm thanh và mở phát được. Nếu vẫn lỗi, gửi đúng file gốc để kiểm tra.\r\nChi tiết: "+e.Message,e);}
}
}
