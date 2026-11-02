using System;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace V15Voice {
// A reversible, deliberately limited grammar. A model's "equivalent" verdict
// is evidence for the report, never permission to invent a proposition.
public static class RepairLanguage {
 public const string Revision="repair-3.0.0-v1";
 static readonly Dictionary<string,string> forms=new Dictionary<string,string>{
  {"can't","can not"},{"cannot","can not"},{"won't","will not"},{"don't","do not"},{"doesn't","does not"},{"didn't","did not"},{"isn't","is not"},{"aren't","are not"},{"wasn't","was not"},{"weren't","were not"},{"hasn't","has not"},{"haven't","have not"},{"hadn't","had not"},{"wouldn't","would not"},{"shouldn't","should not"},{"couldn't","could not"},{"mustn't","must not"},{"we're","we are"},{"they're","they are"},{"you're","you are"},{"i'm","i am"},{"we've","we have"},{"they've","they have"},{"you've","you have"},{"i've","i have"},{"i'll","i will"},{"he'll","he will"},{"she'll","she will"},{"we'll","we will"},{"they'll","they will"},{"you'll","you will"}};
 // 'd + read/cut/put/hit is ambiguous. Do not expand it. With these distinct
 // participles it is HAD; with these base verbs it is WOULD. Preserve adverbs.
 static readonly HashSet<string> participles=new HashSet<string>(("been done gone seen known taken given written eaten drunk spoken spent dreamed dreamt saved left bought found told made lost forgotten kept brought thought felt heard won begun built sold paid chosen become fallen broken").Split(' '));
 static readonly HashSet<string> bases=new HashSet<string>(("ruin need like love prefer want have be do go see know take give write eat drink speak spend dream save leave buy find tell make lose forget keep bring think feel hear win begin build sell pay choose become fall break").Split(' '));
 public static string Expand(string text){
  string value=(text??"").Replace('’','\'');
  foreach(var f in forms.OrderByDescending(x=>x.Key.Length))value=Regex.Replace(value,@"\b"+Regex.Escape(f.Key)+@"\b",m=>Case(m.Value,f.Value),RegexOptions.IgnoreCase);
  return Regex.Replace(value,@"\b(I|we|you|they|he|she|it)'d\b(?<gap>\s+(?:(?:already|just|never|always|also|really|only)\s+)*)(?<verb>[A-Za-z]+)",m=>{string v=m.Groups["verb"].Value.ToLowerInvariant();string aux=participles.Contains(v)?"had":bases.Contains(v)?"would":"";return aux.Length==0?m.Value:Case(m.Groups[1].Value,m.Groups[1].Value.ToLowerInvariant())+" "+aux+m.Groups["gap"].Value+m.Groups["verb"].Value;},RegexOptions.IgnoreCase);
 }
 static string Case(string old,string value){if(old=="I"||old.StartsWith("I'",StringComparison.OrdinalIgnoreCase))return value.Length>0?"I"+value.Substring(1):value;return Char.IsUpper(old[0])?Char.ToUpperInvariant(value[0])+value.Substring(1):value;}
 public static string Key(string text){
  string s=Regex.Replace(Expand(text).ToLowerInvariant(),@"\s+"," ").Trim();
  s=Regex.Replace(s,@"\b(might|may|could|would|will|must|should) be in need of\b","$1 need");
  s=Regex.Replace(s,@"\bmore difficult to\b","harder to");
  s=Regex.Replace(s,@"\bput (our|your|their|my|his|her) (fingers|lives|hands|safety|money) at risk\b","risk $1 $2");
  s=Regex.Replace(s,@"\bthe best feature of the (room|house|building|garden)\b","the $1's best feature");
  s=Regex.Replace(s,@"\b(knew|thought|said) that (?=(?:i|we|you|they|he|she|it|there)\s+(?:could|would|might|had|has|have|is|was|were|can|will|should|must|may|did|does|do)\b)","$1 ");
  // Partitives with an explicit demonstrative/possessive refer to the same set.
  s=Regex.Replace(s,@"\b(all|both) of (our|your|their|my|his|her|these|those|this|that)\b","$1 $2");
  foreach(var f in new[]{new[]{"enormous","huge"},new[]{"massive","huge"},new[]{"entirely","completely"},new[]{"beginning","start"},new[]{"headed","went"},new[]{"concealed","hid"}})s=Regex.Replace(s,@"\b"+f[0]+@"\b",f[1]);
  return s;
 }
 public static bool Equivalent(string before,string after){return Key(before)==Key(after);}
 static IEnumerable<string> Variations(string text,bool expand){
  var choices=new List<string>();if(expand){
   choices.Add(Expand(text));
   choices.Add(Regex.Replace(text,@"\b(all|both) (our|your|their|my|his|her|these|those|this|that)\b","$1 of $2",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\b(might|may|could|would|will|must|should) need (?!to\b)","$1 be in need of ",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\bharder to\b","more difficult to",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\brisk (our|your|their|my|his|her) (fingers|lives|hands|safety|money)\b","put $1 $2 at risk",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\bthe (room|house|building|garden)'s best feature\b","the best feature of the $1",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\b(knew|thought|said) (?=(?:I|we|you|they|he|she|it|there)\s+(?:could|would|might|had|has|have|is|was|were|can|will|should|must|may|did|does|do)\b)","$1 that ",RegexOptions.IgnoreCase));
  }else{
   foreach(var f in forms.Where(x=>x.Key!="cannot"))choices.Add(Regex.Replace(text,@"\b"+Regex.Escape(f.Value)+@"\b",m=>Case(m.Value,f.Key),RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\b(all|both) of (our|your|their|my|his|her|these|those|this|that)\b","$1 $2",RegexOptions.IgnoreCase));
   // The inverse rules have exactly the same grounded key and no new entities.
   choices.Add(Regex.Replace(text,@"\b(might|may|could|would|will|must|should) be in need of\b","$1 need",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\bmore difficult to\b","harder to",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\bput (our|your|their|my|his|her) (fingers|lives|hands|safety|money) at risk\b","risk $1 $2",RegexOptions.IgnoreCase));
   choices.Add(Regex.Replace(text,@"\bthe best feature of the (room|house|building|garden)\b","the $1's best feature",RegexOptions.IgnoreCase));
  }
  return choices.Where(t=>t!=text).Distinct();
 }
 public static string AddedProposition(string before,string after){
  if(Equivalent(before,after))return "";
  // New subordinate/coordinated clauses, including copular facts and mental
  // states, cannot be excused by a positive self-review from the same model.
  var clause=new Regex(@"\b(?:and|but|because|so|while|although|which|who|then)\b",RegexOptions.IgnoreCase);
  if(clause.Matches(after).Count>clause.Matches(before).Count)return "NEW_CLAUSE: thêm mệnh đề/quan hệ chưa có trong lời gốc";
  if(Regex.Matches(after,@"[.!?](?:\s|$)").Count>Regex.Matches(before,@"[.!?](?:\s|$)").Count)return "NEW_SENTENCE: thêm phát biểu chưa có trong lời gốc";
  var a=RepairPolicy.Canonical(before).Split(' ');var b=RepairPolicy.Canonical(after).Split(' ');
  var extra=b.Except(a).ToList();
  if(extra.Any(x=>new[]{"seemed","remained","felt","thought","realized","decided","organized","arranged","shelves"}.Contains(x)))return "NEW_FACT_OR_STATE: thêm trạng thái/hành động/đồ vật cần đối chiếu";
  return "";
 }
 // Generate small *equivalent* alternatives before asking a model to invent
 // length. The measured delta ranks options; it never proves timing success.
 public static List<Cue> Next(AiRepairRow row,AiMeasurement measured){
  var original=row.Stage.Cues;var choices=new List<List<Cue>>();
  foreach(var c in original){
   var texts=Variations(c.Text,measured.Delta<0).ToList();foreach(string first in texts.ToList())texts.AddRange(Variations(first,measured.Delta<0));
   foreach(string t in texts.Where(t=>t!=c.Text).Distinct()){var option=VoiceRenderReport.Snapshot(original);option.Single(x=>x.Id==c.Id).Text=t;choices.Add(option);}
  }
  // If every one-cue alternative has already been measured and is too short,
  // combine only those grammar expansions, in source order (never whole-stage
  // prose rewrites). Limit two cues and six inserted words for a single trial.
  var singles=choices.ToList();for(int i=0;i<singles.Count;i++)for(int j=i+1;j<singles.Count;j++){
   var left=singles[i].Where((c,k)=>c.Text!=original[k].Text).Single();var right=singles[j].Where((c,k)=>c.Text!=original[k].Text).Single();if(left.Id==right.Id)continue;
   var combined=VoiceRenderReport.Snapshot(original);combined.Single(c=>c.Id==left.Id).Text=left.Text;combined.Single(c=>c.Id==right.Id).Text=right.Text;choices.Add(combined);
  }
  double requested=Math.Max(1,Math.Min(6,Math.Abs(measured.Delta)*Math.Max(.9,measured.Speed)*original.Sum(c=>c.Words)/Math.Max(.08,measured.Tts)));
  var observed=row.Attempts.LastOrDefault(a=>a.SemanticSafe&&a.Measurement!=null&&a.Measurement.Valid&&a.Cues.Sum(c=>c.Words)!=original.Sum(c=>c.Words));
  if(observed!=null&&row.Original!=null){double perWord=Math.Abs((observed.Measurement.Natural-row.Original.Natural)/(observed.Cues.Sum(c=>c.Words)-original.Sum(c=>c.Words)));if(perWord>.03)requested=Math.Max(1,Math.Min(6,Math.Abs(measured.Delta)*Math.Max(.9,measured.Speed)/perWord));}
  return choices.Where(c=>RepairPolicy.Check(original,c).Safe&&Math.Abs(c.Sum(x=>x.Words)-original.Sum(x=>x.Words))<=6&&AiRepairSession.SnapshotHash(c)!=AiRepairSession.SnapshotHash(original)&&!row.Attempts.Any(a=>a.Cues.Count>0&&AiRepairSession.SnapshotHash(a.Cues)==AiRepairSession.SnapshotHash(c)))
   .OrderBy(c=>Math.Abs(Math.Abs(c.Sum(x=>x.Words)-original.Sum(x=>x.Words))-requested)).ThenBy(c=>c.Zip(original,(x,y)=>x.Text!=y.Text?1:0).Sum()).FirstOrDefault();
 }
}
}
