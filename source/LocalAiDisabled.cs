using System;using System.IO;using System.Text;using System.Collections.Generic;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;
namespace V15Voice {
[DataContract] public sealed class AiCueText {[DataMember]public int id;[DataMember]public string text;}
[DataContract] public sealed class AiRewriteText {[DataMember]public List<AiCueText> cues;}
public interface ITextRewriter {string Rewrite(string prompt,Func<bool> cancel,Action<string> activity);}
// Read old report formats, but never download or start the removed language model.
public sealed class LocalAi:ITextRewriter {
 public const string ModelName="AI repair removed in Lite",ModelHash="3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597",Revision="disabled",RuntimeRevision="b12019";
 public const long ModelBytes=2497281120;
 public readonly string Root,Device;public string LastDevice="Không dùng AI sửa lời";
 public LocalAi(string root,string device){Root=root;Device=device;}
 public static string Home(string root){return Path.Combine(root,"source","local-ai");}
 public static string Model(string root){return Path.Combine(Home(root),"model","qwen3-4b.gguf");}
 public static string Runtime(string root,bool gpu){return Path.Combine(Home(root),"runtime",gpu?"gpu":"cpu","llama-completion.exe");}
 public static bool Ready(string root){return false;}
 static NotSupportedException Removed(){return new NotSupportedException("Bản Lite không có AI tự sửa/dịch lời. Giữ nguyên nội dung file và xem báo cáo thời lượng.");}
 public static void Setup(string root,Func<bool> cancel,Action<string> activity){throw Removed();}
 public static void InstallModel(string root,string source,Func<bool> cancel){throw Removed();}
 public static void Download(string url,string output,long size,string hash,Func<bool> cancel,Action<string> activity){throw Removed();}
 public static void ExtractRuntime(string zip,string destination,Func<bool> cancel){throw Removed();}
 public static void VerifyFile(string path,long size,string hash,Func<bool> cancel){throw Removed();}
 public string Rewrite(string prompt,Func<bool> cancel,Action<string> activity){throw Removed();}
 public static string Json<T>(T value){using(var ms=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(ms,value);return Encoding.UTF8.GetString(ms.ToArray());}}
 public static T Parse<T>(string value){if(value==null||value.Length>64000)throw new Exception("Phản hồi AI rỗng hoặc quá dài.");value=System.Text.RegularExpressions.Regex.Replace(value,@"<think>[\s\S]*?</think>","");int start=value.IndexOf('{');if(start<0)throw new Exception("AI chưa trả JSON hợp lệ.");bool quoted=false,escape=false;int depth=0,end=-1;for(int i=start;i<value.Length;i++){char c=value[i];if(quoted){if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='"')quoted=false;}else if(c=='"')quoted=true;else if(c=='{')depth++;else if(c=='}'&&--depth==0){end=i+1;break;}}if(end<0)throw new Exception("Phản hồi AI chưa hoàn chỉnh.");string suffix=value.Substring(end).Trim();if(suffix.Contains("{")||suffix.Contains("}"))throw new Exception("AI trả nhiều đối tượng JSON; từ chối phản hồi mơ hồ.");using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(value.Substring(start,end-start))))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
}
}
