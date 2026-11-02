using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace V15Voice {
public sealed class CreatorMediaInfo {public double Duration,Fps;public int Width,Height;public string Codec="",Rotation="";public bool Audio;}
public sealed class CreatorTools {
 public string Encoder="",Filters="",Muxers="",Exe="";
 public bool Mux {get{return Regex.IsMatch(Encoder,@"\baac\b")&&Regex.IsMatch(Muxers,@"\bmp4\b");}}
 public bool Draw {get{return Regex.IsMatch(Encoder,@"\blibx264\b")&&Regex.IsMatch(Filters,@"\bass\b");}}
}
public static class CreatorMedia {
 public static string Tool(string root){string own=Path.Combine(root,"source","creator-tools","ffmpeg.exe");return Environment.OSVersion.Platform==PlatformID.Win32NT&&File.Exists(own)?own:Tempo.Executable(root);}
 public static string N(double n){return n.ToString("0.000000",CultureInfo.InvariantCulture);}
 public static string Run(string exe,string args,Func<bool> cancel,Action<double> progress=null,string work=null,int timeout=7200000){
  if(cancel())throw new OperationCanceledException();var log=new StringBuilder();var gate=new object();Exception callbackError=null;
  var pi=new ProcessStartInfo(exe,"-nostdin -hide_banner "+args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};if(work!=null)pi.WorkingDirectory=work;
  using(var p=new Process{StartInfo=pi}){
   p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(gate){log.AppendLine(e.Data);if(log.Length>64000)log.Remove(0,log.Length-64000);}};
   p.OutputDataReceived+=(s,e)=>{if(e.Data==null)return;double n;if(e.Data.StartsWith("out_time_us=")&&Double.TryParse(e.Data.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&progress!=null){try{progress(n/1000000.0);}catch(Exception ex){lock(gate){callbackError=ex;}}}else lock(gate){log.AppendLine(e.Data);if(log.Length>64000)log.Remove(0,log.Length-64000);}};
   p.Start();p.BeginErrorReadLine();p.BeginOutputReadLine();var clock=Stopwatch.StartNew();
   while(!p.WaitForExit(100)){Exception failure;lock(gate){failure=callbackError;}if(failure!=null||cancel()||clock.ElapsedMilliseconds>timeout){try{p.Kill();p.WaitForExit(2000);}catch{}if(failure!=null)throw new IOException("CREATOR_PROGRESS_FAILED: không lưu/cập nhật được tiến độ.",failure);if(cancel())throw new OperationCanceledException();throw new TimeoutException("CREATOR_TIMEOUT: xử lý video quá thời gian cho phép.");}}
   p.WaitForExit();if(callbackError!=null)throw new IOException("CREATOR_PROGRESS_FAILED: không lưu/cập nhật được tiến độ.",callbackError);if(cancel())throw new OperationCanceledException();if(p.ExitCode!=0){var ex=new IOException("CREATOR_MEDIA_ERROR: "+log.ToString().Substring(Math.Max(0,log.Length-3000)));ex.Data["ProcessExitCode"]=p.ExitCode;ex.Data["Process"]=exe;throw ex;}return log.ToString();
  }
 }
 public static CreatorTools Capabilities(string root,Func<bool> cancel){string exe=Tool(root);return new CreatorTools{Exe=exe,Encoder=Run(exe,"-v error -encoders",cancel,timeout:30000),Muxers=Run(exe,"-v error -muxers",cancel,timeout:30000),Filters=Run(exe,"-v error -filters",cancel,timeout:30000)};}
 public static CreatorMediaInfo Parse(string raw){
  // Limit parsing to the INPUT section: output/muxer dimensions are not source measurements.
  int at=raw.IndexOf("Output #",StringComparison.Ordinal);if(at>=0)raw=raw.Substring(0,at);
  var d=Regex.Match(raw,@"Duration:\s*(\d+):(\d\d):(\d\d(?:\.\d+)?)");
  var v=Regex.Match(raw,@"Stream[^\r\n]*Video:\s*(\w+)[^\r\n]*");
  if(!d.Success||!v.Success)throw new IOException("VIDEO_PROBE_FAILED: chưa đo được thời lượng hoặc track hình. Chọn video hoàn chỉnh có timecode.");
  var size=Regex.Match(v.Value,@"(?:,\s*|\s)(\d{2,5})x(\d{2,5})(?=\s|,|\[)");if(!size.Success)throw new IOException("VIDEO_PROBE_FAILED: chưa xác nhận kích thước hình; cần FFmpeg có parser/decoder cho codec này.");
  var fps=Regex.Match(v.Value,@"([\d.]+)\s+fps");var rotation=Regex.Match(raw,@"rotation of\s+(-?[\d.]+)|rotate\s*:\s*(-?[\d.]+)");
  return new CreatorMediaInfo{Duration=Double.Parse(d.Groups[1].Value,CultureInfo.InvariantCulture)*3600+Double.Parse(d.Groups[2].Value,CultureInfo.InvariantCulture)*60+Double.Parse(d.Groups[3].Value,CultureInfo.InvariantCulture),Width=Int32.Parse(size.Groups[1].Value),Height=Int32.Parse(size.Groups[2].Value),Codec=v.Groups[1].Value.ToLowerInvariant(),Fps=fps.Success?Double.Parse(fps.Groups[1].Value,CultureInfo.InvariantCulture):0,Audio=Regex.IsMatch(raw,@"Stream[^\r\n]*Audio:"),Rotation=rotation.Success?rotation.Value:""};
 }
 public static CreatorMediaInfo Probe(string root,string file,Func<bool> cancel){if(!File.Exists(file))throw new IOException("VIDEO_MISSING: thiếu video gốc.");return Parse(Run(Tool(root),"-i "+KokoroEngine.Q(Path.GetFullPath(file))+" -map 0:v:0 -map 0:a:0? -t 0.001 -c copy -f null -",cancel,timeout:30000));}
 public static void Import(string root,string chosen,Func<bool> cancel){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new IOException("Nhập FFmpeg Windows chỉ dùng trên Windows.");
  string enc=Run(chosen,"-v error -encoders",cancel,timeout:30000),mux=Run(chosen,"-v error -muxers",cancel,timeout:30000),filters=Run(chosen,"-v error -filters",cancel,timeout:30000);
  if(!Regex.IsMatch(enc,@"\baac\b")||!Regex.IsMatch(mux,@"\bmp4\b")||!Regex.IsMatch(filters,@"\bapad\b"))throw new IOException("FFMPEG_INCOMPLETE: cần bản FFmpeg Windows static có AAC, MP4 và apad. Không thay bộ audio hiện tại.");
  string dir=Path.Combine(root,"source","creator-tools");Directory.CreateDirectory(dir);string target=Path.Combine(dir,"ffmpeg.exe"),temp=Path.Combine(dir,Guid.NewGuid().ToString("N")+".exe");try{CreatorFiles.Copy(chosen,temp,cancel);Run(temp,"-v error -encoders",cancel,timeout:30000);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{try{File.Delete(temp);}catch{}}
 }
 public static void Render(string root,CreatorProject p,CreatorAudit a,string output,string work,Func<bool> cancel,Action<int,string> progress){
  var tools=Capabilities(root,cancel);if(!tools.Mux)throw new IOException("CREATOR_TOOLS_REQUIRED: bộ audio hiện tại chưa có mã hóa AAC/MP4. Nhập FFmpeg Windows static đầy đủ ở Creator → Thành phần video, hoặc chọn Gói dựng.");
  bool encode=p.BurnCaptions||p.Vertical||a.Video.Codec!="h264";
  if(encode&&!Regex.IsMatch(tools.Encoder,@"\blibx264\b"))throw new IOException("VIDEO_ENCODER_MISSING: tùy chọn này cần libx264. Có thể chọn giữ nguyên hình / Gói dựng.");
  if(p.BurnCaptions&&!tools.Draw)throw new IOException("CAPTION_ENCODER_MISSING: in phụ đề cần libass (ass filter) và libx264.");
  if(a.Video.Rotation.Length>0&&encode)throw new IOException("ROTATION_REVIEW_REQUIRED: video có metadata xoay; hãy chuẩn hóa hướng trước khi in chữ/chuyển khung.");
  if(Double.IsNaN(p.OriginalGain)||Double.IsInfinity(p.OriginalGain)||p.OriginalGain<0||p.OriginalGain>.3)throw new IOException("ORIGINAL_GAIN_INVALID: âm thanh gốc chỉ được trộn trong khoảng 0–30%.");
  string filter="",audio;
  if(p.MixOriginal&&a.Video.Audio)audio="[0:a:0]volume="+N(p.OriginalGain)+"[bed];[1:a:0][bed]amix=inputs=2:duration=longest:normalize=0,alimiter=level=false:limit=0.95,apad=pad_dur=0.150,atrim=end="+N(a.Video.Duration)+",aresample=48000[a]";
  else audio="[1:a:0]apad=pad_dur=0.150,atrim=end="+N(a.Video.Duration)+",aresample=48000[a]";
  if(p.Vertical)filter="scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2,setsar=1";
  if(p.BurnCaptions){File.WriteAllText(Path.Combine(work,"captions.ass"),CreatorCaptions.Ass(a.Cues,p.Vertical?1080:a.Video.Width,p.Vertical?1920:a.Video.Height),new UTF8Encoding(false));filter+=(filter.Length>0?",":"")+"ass=filename=captions.ass";}
  string args="-v info -nostats -n -i "+KokoroEngine.Q(p.Video)+" -i "+KokoroEngine.Q(p.Voice)+" -filter_complex "+KokoroEngine.Q(audio)+" -map 0:v:0 -map "+KokoroEngine.Q("[a]")+" -map_metadata -1 -map_chapters -1 ";
  args+=encode?"-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p ":"-c:v copy ";if(filter.Length>0)args+="-vf "+KokoroEngine.Q(filter)+" ";
  args+="-c:a aac -b:a 192k -ar 48000 -ac 2 -t "+N(a.Video.Duration)+" -movflags +faststart -progress pipe:1 "+KokoroEngine.Q(output);
  progress(25,"Ghép MP4 · "+(encode?"mã hóa hình CPU":"giữ nguyên luồng hình"));Run(tools.Exe,args,cancel,s=>progress(25+(int)Math.Min(59,59*s/a.Video.Duration),"Ghép MP4 · "+s.ToString("0.0")+" / "+a.Video.Duration.ToString("0.0")+"s"),work);
  var measured=Probe(root,output,cancel);if(!measured.Audio||Math.Abs(measured.Duration-a.Video.Duration)>.15)throw new IOException("MP4_VERIFY_FAILED: MP4 chưa có audio hoặc thời lượng lệch quá 0,150s.");
  // Decode the complete output to catch corrupt packets; container duration alone is insufficient.
  Run(tools.Exe,"-v error -xerror -i "+KokoroEngine.Q(output)+" -map 0:v:0 -map 0:a:0 -f null -",cancel,timeout:7200000);progress(87,"MP4 đã đo và giải mã kiểm tra");
 }
}
public static class CreatorCaptions {
 public static string[] Lines(string text,int width=38){var words=Regex.Split(text.Trim(),@"\s+");var lines=new List<string>();string row="";foreach(string w in words){if(row.Length>0&&row.Length+1+w.Length>width){lines.Add(row);row="";}row+=(row.Length>0?" ":"")+w;}if(row.Length>0)lines.Add(row);return lines.ToArray();}
 static string Stamp(double s){long cs=(long)Math.Round(s*100);return (cs/360000)+":"+(cs/6000%60).ToString("00")+":"+(cs/100%60).ToString("00")+"."+(cs%100).ToString("00");}
 public static string Ass(List<Cue> cues,int width,int height){
  int font=Math.Max(16,(int)Math.Round(width*.041)),bottom=(int)Math.Round(height*.22),left=(int)Math.Round(width*.07),right=(int)Math.Round(width*.16);
  var b=new StringBuilder("[Script Info]\r\nScriptType: v4.00+\r\nPlayResX: "+width+"\r\nPlayResY: "+height+"\r\nWrapStyle: 2\r\nScaledBorderAndShadow: yes\r\n\r\n[V4+ Styles]\r\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\r\nStyle: Creator,Arial,"+font+",&H00FFFFFF,&H00FFFFFF,&H00141414,&H60000000,-1,0,0,0,100,100,0,0,1,2,1,2,"+left+","+right+","+bottom+",1\r\n\r\n[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n");
  foreach(var c in cues){var lines=Lines(c.Text);if(lines.Length>2)throw new IOException("CAPTION_TOO_LONG: cue "+c.Id+" vượt 2 dòng; chia phụ đề trước hoặc xuất SRT rời. Không tự rút lời voice.");string safe=String.Join("\\N",lines.Select(l=>l.Replace("\\","＼").Replace("{","｛").Replace("}","｝")));b.Append("Dialogue: 0,").Append(Stamp(c.Start)).Append(',').Append(Stamp(c.End)).Append(",Creator,,0,0,0,,").AppendLine(safe);}return b.ToString();
 }
}
}
