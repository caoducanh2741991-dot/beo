using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;
namespace V15Voice {
public static class StableArtifacts {
 static string Words(string s){return System.Text.RegularExpressions.Regex.Replace(s??"",@"\s+"," ").Trim();}
 public static List<Cue> FinalCues(List<Cue> cues,RenderOptions o,double duration){
  if(o.Prose&&o.TargetSeconds>0)return new List<Cue>{new Cue{Id=1,Start=0,End=duration,Text=String.Join(" ",cues.Select(c=>c.Text))}};
  return VoiceRenderReport.Snapshot(cues);
 }
 public static string Bilingual(List<Cue> cues,bool anchored,bool mixedWhole=false,TranslationResult translation=null){var b=new StringBuilder();b.AppendLine("V15 2.0.16 · ĐỐI CHIẾU LỜI CUỐI ĐÃ DÙNG TẠO WAV");b.AppendLine("DỊCH VIỆT: "+(translation!=null&&translation.Complete?"TRANSLATION_COMPLETE · "+translation.Model+" · đối chiếu cấu trúc/số liệu/model; cần đọc lại chất lượng":"TRANSLATION_INCOMPLETE · bộ đầu ra chưa hoàn thiện; "+(translation==null?"chưa chạy":translation.Error)));b.AppendLine(anchored?"Mốc dưới đây là TIMELINE SRT NGUỒN; chưa có mốc WAV riêng từng cue sau căn chặng.":"Mốc cue theo chế độ xuất; không phải căn từng từ bằng nhận dạng giọng nói.");foreach(var c in cues){b.AppendLine();b.AppendLine("#"+c.Id+" | "+Scripts.Stamp(c.Start)+" --> "+Scripts.Stamp(c.End)+(mixedWhole?" | A/B theo báo cáo; khối chung, chưa có mốc từng lượt sau căn":" | Giọng "+SpeakerScript.Role(c)));b.AppendLine("EN: "+c.Text);b.AppendLine("VI: "+(translation!=null&&translation.Text.ContainsKey(c.Id)?translation.Text[c.Id]:"[TRANSLATION_INCOMPLETE]"));}return b.ToString();}
 static string ChangedAgainstSource(List<Cue> used,RenderOptions o){try{string path=o.ReportContext==null?"":o.ReportContext.SourcePath;if(!File.Exists(path))return "chưa có file nguồn để đối chiếu";string data=File.ReadAllText(path);var original=data.Contains("-->")?Scripts.ParseSrt(data):o.Prose?LongText.Parse(data):Scripts.ParseText(data);if(original.Count!=used.Count)return "không gán theo cue do đã gộp/tách đơn vị; xem lời cuối";return original.Zip(used,(a,b)=>a.Text!=b.Text?1:0).Sum().ToString();}catch{return "chưa xác định";}}
 public static void VerifyWords(List<Cue> cues,VoiceRenderReport report){if(report==null)throw new IOException("Không có báo cáo để xác nhận lời đã tạo voice.");string used=Words(String.Join(" ",report.Units.Select(u=>u.Text))),expected=Words(String.Join(" ",cues.Select(c=>c.Text)));if(used!=expected)throw new IOException("Không xuất: lời trong các đơn vị TTS khác kịch bản cuối. Cần kiểm tra ánh xạ lời.");}
 public static string Publish(string raw,string requested,List<Cue> cues,RenderOptions o,VoiceRenderReport report,Action<int> fault=null){
  VerifyWords(cues,report);report.VerifySourceUnchanged();if(o.AiRepair!=null)o.AiRepair.Validate();double duration=FinalAudio.PcmFrames(raw)/24000.0;var final=FinalCues(cues,o,duration);Scripts.Validate(final);string srt=Scripts.ToSrt(final);var parsed=Scripts.ParseSrt(srt);if(!parsed.Select(c=>c.Text).SequenceEqual(final.Select(c=>c.Text)))throw new IOException("SRT cuối không giữ đúng lời TTS.");
  if(o.Translation!=null&&o.Translation.Complete)try{if(o.Translation.SourceHash!=AiRepairSession.SnapshotHash(final)||o.Translation.Text.Count!=final.Count||!final.All(c=>o.Translation.Text.ContainsKey(c.Id)))throw new IOException("Translation snapshot/IDs khác EN cuối.");foreach(var c in final)try{OfflineTranslation.Validate(c.Text,o.Translation.Text[c.Id]);}catch{o.Translation.Text.Remove(c.Id);throw;}}catch(Exception ex){o.Translation.Status="TRANSLATION_INCOMPLETE";o.Translation.Error="Kiểm tra bộ cuối: "+ex.Message;o.Translation.Stack=ex.ToString();}
  string dir=Path.GetDirectoryName(Path.GetFullPath(requested)),basis=Path.GetFileNameWithoutExtension(requested);string aligned=o.AnchorResult==null?"":o.AnchorResult.Srt;
  var endings=new List<string>{".final.srt",".final.vi.txt",".ai-repair-report.txt",".voice-report.txt",".wav"};if(!String.IsNullOrEmpty(aligned)&&File.Exists(aligned))endings.Insert(0,".aligned.srt");
  for(int n=0;n<10000;n++){
   string stem=Path.Combine(dir,basis+(n==0?"":" ("+(n+1)+")"));if(endings.Any(ext=>File.Exists(stem+ext)||Directory.Exists(stem+ext)))continue;
   string lockPath=stem+".v15-publish.lock";FileStream reserved;try{reserved=new FileStream(lockPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);}catch(IOException){continue;}
   var created=new List<string>();string stage=Path.Combine(dir,".v15_publish_"+Guid.NewGuid().ToString("N"));
   try{using(reserved){if(endings.Any(ext=>File.Exists(stem+ext)||Directory.Exists(stem+ext)))continue;Directory.CreateDirectory(stage);
     string target=stem+".wav";if(o.AiRepair!=null){o.AiRepair.Reconcile(cues,report);o.AiRepair.Output=target;o.AiRepair.TranslationStatus=o.Translation!=null?o.Translation.Status:"TRANSLATION_INCOMPLETE";o.AiRepair.Outcome=o.Translation!=null&&o.Translation.Complete?"FINAL_VERIFIED · WAV/SRT/TXT đồng bộ; TRANSLATION_COMPLETE":"TRANSLATION_INCOMPLETE · chỉ WAV_FINAL_VERIFIED; bộ đầu ra chưa hoàn thiện";}
     report.Step("SYNC","Xác nhận lời TTS, SRT, TXT và báo cáo cùng một lần xuất");if(report.FailureCode=="TRANSLATION_INCOMPLETE")report.FailureCode="";report.Finish("THÀNH CÔNG",target,null);report.PathName=stem+".voice-report.txt";if(o.Translation==null||!o.Translation.Complete){report.Outcome="TRANSLATION_INCOMPLETE · WAV_FINAL_VERIFIED";report.FailureCode="TRANSLATION_INCOMPLETE";}
     var utf8=new UTF8Encoding(false);File.WriteAllText(Path.Combine(stage,".final.srt"),srt,utf8);File.WriteAllText(Path.Combine(stage,".final.vi.txt"),Bilingual(final,o.Anchored,o.TwoVoices&&o.Prose&&o.TargetSeconds>0,o.Translation),utf8);
     string changed=ChangedAgainstSource(cues,o);string ai=o.AiRepair!=null?o.AiRepair.BuildReport():"V15 AI REPAIR REPORT · 2.0.16\r\nTrạng thái WAV: FINAL_VERIFIED · PASS\r\nTrạng thái bộ đầu ra: "+(o.Translation!=null&&o.Translation.Complete?"FINAL_VERIFIED · TRANSLATION_COMPLETE":"TRANSLATION_INCOMPLETE")+"\r\nAI không thay đổi lời trong lần này.\r\nSố cue thay đổi lời bởi AI: 0\r\nWAV: "+target+"\r\nLời cuối: "+AiRepairSession.SnapshotHash(final)+"\r\n";
     if(o.AiRepair==null){foreach(var st in report.Stages)ai+="\r\nChặng "+st.Number+" · cue "+String.Join(",",st.CueIds)+" · FINAL_VERIFIED · PASS · "+Scripts.Stamp(st.Start)+" → "+Scripts.Stamp(st.End)+" · trước "+st.Before+"s · sau "+st.After+"s · "+RepairPolicy.Quality(st.Speed??0);if(report.Stages.Count==0)foreach(var u in report.Units)ai+="\r\nCue "+String.Join(",",u.CueIds)+" · FINAL_VERIFIED · PASS";}ai+="\r\nTổng cue khác file nguồn: "+changed+"\r\nWAV SHA256: "+StateFiles.Hash(raw)+"\r\n";
     File.WriteAllText(Path.Combine(stage,".ai-repair-report.txt"),ai,utf8);File.WriteAllText(Path.Combine(stage,".voice-report.txt"),report.Build()+"\r\nTổng cue khác file nguồn: "+changed+"\r\nBỘ ĐẦU RA ĐỒNG BỘ: "+stem+".final.srt; "+stem+".final.vi.txt\r\nDịch Việt: "+(o.Translation==null?"TRANSLATION_INCOMPLETE":o.Translation.Status)+"; "+(o.Translation==null?"":o.Translation.Error)+"\r\n",utf8);
     if(endings.Contains(".aligned.srt"))File.Copy(aligned,Path.Combine(stage,".aligned.srt"));File.Copy(raw,Path.Combine(stage,".wav"));
     // WAV is the commit marker: it appears only after every sidecar is ready.
     foreach(string ext in endings){File.Move(Path.Combine(stage,ext),stem+ext);created.Add(stem+ext);if(fault!=null)fault(created.Count);}
     if(o.AiRepair!=null)o.AiRepair.Save();if(o.AnchorResult!=null){o.AnchorResult.Wav=target;if(endings.Contains(".aligned.srt"))o.AnchorResult.Srt=stem+".aligned.srt";}if(o.TargetResult!=null)o.TargetResult.Wav=target;
     report.ArtifactPublished();return target;
    }
   }catch{foreach(string path in created)try{File.Delete(path);}catch{}throw;}
   finally{try{if(Directory.Exists(stage))Directory.Delete(stage,true);}catch{}try{File.Delete(lockPath);}catch{}}
  }
  throw new IOException("Không tìm được tên cho bộ đầu ra.");
 }
}
}
