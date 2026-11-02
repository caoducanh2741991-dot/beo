using System;using System.Linq;using System.Collections.Generic;
namespace V15Voice {
// Only adjust zero samples inserted by the app BETWEEN known pre-fit units.
// Never edit speech, a breath sample, native model silence, or stage edges.
public static class NaturalPauseRepair {
 // The measurement path and the final renderer must use the same acoustic
 // repair policy. Only bounded, known app pauses are eligible; speech and
 // native silence are untouched, and a retry must be measured again.
 public static AnchorFit Fit(ref Wave natural,List<AnchorUnit> units,double target,RenderOptions o,string root,string work,Func<bool> cancel,out double added,out List<double> at,out string note,out double beforeAfter){
  added=0;at=new List<double>();note="";var fit=AnchoredVoice.Fit(natural,target,o,root,work,cancel);beforeAfter=fit.Audio==null?0:fit.Audio.Duration;if(fit.Success)return fit;
  double change;List<double> points;var adjusted=Adjust(natural,units,(target-fit.Measured)*fit.Speed/o.BaseSpeed,out change,out points);
  note="Không đủ khoảng nghỉ app hợp lệ; không đệm đuôi để giả đạt.";if(adjusted==null)return fit;
  var retry=AnchoredVoice.Fit(adjusted,target,o,root,work,cancel);note="Thử cân "+change.ToString("+0.000;-0.000;0",System.Globalization.CultureInfo.InvariantCulture)+"s tại "+points.Count+" ranh giới tự nhiên; tối đa 0.08s/vị trí, 0.24s/chặng, 2% tổng; "+(retry.Success?"đo đạt":"chưa đạt, không áp dụng");
  if(!retry.Success)return fit;added=change;at=points;natural=adjusted;
  foreach(var u in units){double start=u.Start,end=u.End;u.Start+=points.Count(p=>p<=start)*added/points.Count;u.End+=points.Count(p=>p<=end)*added/points.Count;}
  retry.Reason=note;return retry;
 }
 public static BreathState Shift(BreathState state,double stageStart,double added,List<double> positions){var shifted=state.Copy();if(added!=0&&positions.Count>0&&shifted.Last>=stageStart)shifted.Last+=positions.Count(x=>stageStart+x<=shifted.Last)*added/positions.Count;return shifted;}
 public static Wave Adjust(Wave input,List<AnchorUnit> units,double requested,out double actual,out List<double> positions){
  actual=0;positions=new List<double>();if(units.Count<2||Math.Abs(requested)<.002)return null;
  int rate=input.Rate;double limit=Math.Min(.24,Math.Min(input.Duration*.02,Math.Abs(requested)));
  var spans=new List<Tuple<int,int>>();
  for(int u=1;u<units.Count;u++){
   if(units[u].AppPauseBefore<=0)continue;
   int from=Math.Max(0,(int)Math.Round(Math.Max(units[u-1].End,units[u].Start-units[u].AppPauseBefore)*rate)),to=Math.Min(input.Samples.Length,(int)Math.Round(units[u].Start*rate));int best=0,start=from,run=0,rs=from;
   for(int i=from;i<to;i++){if(input.Samples[i]==0){if(run==0)rs=i;run++;if(run>best){best=run;start=rs;}}else run=0;}
   if(best>=rate*.015)spans.Add(Tuple.Create(start,best));
  }
  if(spans.Count==0)return null;int each=(int)Math.Round(Math.Min(.08,limit/spans.Count)*rate)*(requested<0?-1:1);if(each<0)each=-Math.Min(-each,spans.Min(x=>Math.Max(0,x.Item2-(int)(.025*rate))));if(each==0)return null;
  var pieces=new List<short>();int cursor=0;
  foreach(var span in spans){int delta=each;if(delta<0)delta=-Math.Min(-delta,Math.Max(0,span.Item2-(int)(.025*rate)));int at=delta<0?span.Item1:span.Item1+span.Item2/2;
   if(delta==0)continue;pieces.AddRange(input.Samples.Skip(cursor).Take(at-cursor));if(delta>0){pieces.AddRange(new short[delta]);cursor=at;}else cursor=at-delta;
   actual+=delta/(double)rate;positions.Add(at/(double)rate);
  }
  pieces.AddRange(input.Samples.Skip(cursor));return actual==0?null:new Wave{Rate=rate,Samples=pieces.ToArray()};
 }
}
}
