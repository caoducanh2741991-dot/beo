using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;
namespace V15Voice {
public enum PunctuationMode {Auto,Custom}
public sealed class SpeechPart {public string Text;public double Pause;}
public sealed class PacedSpeech {
 public readonly List<Wave> Parts=new List<Wave>();public readonly List<double> Pauses=new List<double>();
 public double SpeechSeconds{get{return Parts.Sum(w=>w.Duration);}}public double PauseSeconds{get{return Pauses.Sum();}}
 public Wave Join(double scale=1){int rate=24000,total=Parts.Sum(w=>w.Resample(rate).Samples.Length)+Pauses.Sum(p=>(int)Math.Round(p*scale*rate));var data=new short[total];int at=0;for(int i=0;i<Parts.Count;i++){var w=Parts[i].Resample(rate);Array.Copy(w.Samples,0,data,at,w.Samples.Length);at+=w.Samples.Length;if(i<Pauses.Count)at+=(int)Math.Round(Pauses[i]*scale*rate);}return new Wave{Rate=rate,Samples=data};}
}
public static class Punctuation {
 static readonly Regex Abbreviation=new Regex(@"(?:\b(?:Mr|Mrs|Ms|Dr|Prof|Sr|Jr|St|vs|etc|Fig|No|U\.S|U\.K|e\.g|i\.e)|\b[A-Za-z])\.$",RegexOptions.IgnoreCase);
 public static void Validate(RenderOptions o){foreach(double n in new[]{o.CommaPause,o.StopPause,o.QuestionPause,o.ParagraphPause})if(Double.IsNaN(n)||Double.IsInfinity(n)||n<0||n>1.5)throw new Exception("Khoảng nghỉ phải nằm trong 0–1,50 giây.");}
 public static List<SpeechPart> Split(string text,RenderOptions o){Validate(o);var parts=new List<SpeechPart>();int start=0;
  for(int i=0;i<text.Length;i++){char c=text[i];double pause=0;int end=i+1;
   if(c=='\n'){while(end<text.Length&&Char.IsWhiteSpace(text[end]))end++;pause=o.ParagraphPause;}
   else if(",;:.?!".IndexOf(c)>=0){if((c=='.'||c==',')&&i>0&&i+1<text.Length&&Char.IsDigit(text[i-1])&&Char.IsDigit(text[i+1]))continue;if(c=='.'&&Abbreviation.IsMatch(text.Substring(start,end-start)))continue;
    while(end<text.Length&&".!?\"”’')]}".IndexOf(text[end])>=0)end++;if(end<text.Length&&!Char.IsWhiteSpace(text[end]))continue;
    pause=c==','?o.CommaPause:(c=='?'||c=='!')?o.QuestionPause:o.StopPause;int next=end;while(next<text.Length&&Char.IsWhiteSpace(text[next])){if(text[next]=='\n')pause=o.ParagraphPause;next++;}
   }else continue;
   string value=text.Substring(start,end-start).Trim();if(value.Length==0)continue;parts.Add(new SpeechPart{Text=value,Pause=pause});start=end;i=end-1;
  }string tail=text.Substring(start).Trim();if(tail.Length>0)parts.Add(new SpeechPart{Text=tail});if(parts.Count>0)parts[parts.Count-1].Pause=0;return parts;
 }
 public static double After(string text,RenderOptions o){if(o.Punctuation!=PunctuationMode.Custom)return 0;Validate(o);string t=(text??"").TrimEnd(' ','\t','\r','\n','\"','”','’','\'',')',']','}');if(t.Length==0)return 0;char c=t[t.Length-1];if(c==',')return o.CommaPause;if(c==';'||c==':')return o.StopPause;if(c=='?'||c=='!')return o.QuestionPause;if(c=='.'&&!Abbreviation.IsMatch(t))return o.StopPause;return 0;}
 public static double EdgeSilence(Wave w,bool leading){int n=0;for(int i=0;i<w.Samples.Length;i++){int at=leading?i:w.Samples.Length-1-i;if(Math.Abs((int)w.Samples[at])>=45)break;n++;}return n/(double)w.Rate;}
 public static PacedSpeech Generate(KokoroEngine e,string text,double speed,RenderOptions o,string output){var p=new PacedSpeech();if(o.Punctuation==PunctuationMode.Auto){p.Parts.Add(e.GetSpeech(text,speed,output).Resample(24000));return p;}var plan=Split(text,o);if(plan.Count==0)throw new Exception("Chưa có lời đọc.");
  for(int i=0;i<plan.Count;i++){if(e.Cancelled)throw new OperationCanceledException();p.Parts.Add(e.GetSpeech(plan[i].Text,speed,output).Resample(24000));if(i<plan.Count-1)p.Pauses.Add(Math.Max(0,plan[i].Pause-EdgeSilence(p.Parts[i],false)));}for(int i=0;i<p.Pauses.Count;i++)p.Pauses[i]=Math.Max(0,p.Pauses[i]-EdgeSilence(p.Parts[i+1],true));return p;
 }
 public static Wave Fit(PacedSpeech p,Cue cue,RenderOptions o,string root,string work,Func<bool> cancel,ref double reserve){double scale=1,available=Math.Max(.04,cue.Duration-.025);if(o.Punctuation==PunctuationMode.Custom&&p.PauseSeconds+reserve>0&&p.SpeechSeconds+p.PauseSeconds+reserve>available){scale=Math.Max(0,Math.Min(1,(available-p.SpeechSeconds)/(p.PauseSeconds+reserve)));reserve*=scale;}
  if(o.RunReport!=null)o.RunReport.Pacing(scale,reserve);
  var budget=new Cue{Id=cue.Id,Start=cue.Start,End=cue.End-reserve,Text=cue.Text};var options=new RenderOptions{BaseSpeed=o.BaseSpeed,MaxSpeed=o.MaxSpeed,MinSpeed=o.MinSpeed,SlowShort=o.SlowShort,GentleFit=o.GentleFit,RunReport=o.RunReport};var w=Tempo.Fit(p.Join(scale),budget,options,root,work,cancel);cue.Speed=budget.Speed;cue.Actual=w.Duration;return w;
 }
}
}
