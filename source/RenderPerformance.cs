using System;using System.Collections.Generic;using System.Diagnostics;using System.Text;
namespace V15Voice {
// Timing stays inside the existing report. Thread-local ownership keeps parallel
// jobs/tests independent and does not route audio or change synthesis settings.
public sealed class RenderPerformance {
 [ThreadStatic]static RenderPerformance current;public static RenderPerformance Current{get{return current;}}
 readonly Stopwatch total=Stopwatch.StartNew();readonly Dictionary<string,double> seconds=new Dictionary<string,double>();readonly Dictionary<string,int> counts=new Dictionary<string,int>();
 sealed class Restore:IDisposable {readonly RenderPerformance previous;public Restore(RenderPerformance old){previous=old;}public void Dispose(){current=previous;}}
 sealed class Span:IDisposable {readonly RenderPerformance owner;readonly string name;readonly Stopwatch watch=Stopwatch.StartNew();bool ended;public Span(RenderPerformance p,string key){owner=p;name=key;}public void Dispose(){if(ended)return;ended=true;owner.Add(name,watch.Elapsed.TotalSeconds);}}
 public IDisposable Activate(){var old=current;current=this;return new Restore(old);}
 public IDisposable Time(string name){return new Span(this,name);}
 void Add(string key,double value){lock(seconds){double v;seconds.TryGetValue(key,out v);seconds[key]=v+value;}}
 public void Count(string key){lock(seconds){int value;counts.TryGetValue(key,out value);counts[key]=value+1;}}
 public string Describe(){lock(seconds){var b=new StringBuilder("THỜI GIAN XỬ LÝ THỰC CỦA LẦN NÀY (không phải tốc độ đọc)\r\n");b.Append("Đã đo đến lúc tạo báo cáo: ").Append(total.Elapsed.TotalSeconds.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)).AppendLine("s; chưa gồm ghi/công bố bộ file cuối. Các bước lồng nhau, không cộng để suy ra tổng.");foreach(var item in seconds)b.Append(item.Key).Append(": ").Append(item.Value.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)).AppendLine("s");foreach(var item in counts)b.Append(item.Key).Append(": ").Append(item.Value).AppendLine(" lượt");return b.ToString();}}
}
}
