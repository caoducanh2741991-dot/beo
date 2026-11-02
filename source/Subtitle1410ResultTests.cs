using System;using System.IO;using System.Linq;using V15Voice;using System.Xml.Linq;
public static class Subtitle1410ResultTests {
 static string Word(XElement w){return String.Join("|",w.Attributes().OrderBy(a=>a.Name.ToString()).Select(a=>a.Name+"="+a.Value))+"/"+w.Value;}
 public static void Main(string[] args){
 foreach(string file in args){var d=SubtitleDocument.Load(file);d.Validate();if(d.Pipeline!="v1410-speech-1"||d.WordTimings==null)throw new Exception("Actual production word metadata missing");
 string root=Path.Combine(Path.GetTempPath(),"v1410-result-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string media=Path.Combine(root,"User original.mp4");File.WriteAllText(media,"KEEP");string first=d.ExportBeside(media),second=d.ExportBeside(media);if(Path.GetFileName(first)!="User original.srt"||!second.EndsWith("_2.srt")||File.ReadAllText(media)!="KEEP")throw new Exception("Adjacent output preservation failed");
 string project=Path.Combine(root,"project.xml");d.Save(project);var restored=SubtitleDocument.Load(project);if(restored.Repetitions!=d.Repetitions||!restored.WordTimings.Elements("word").Select(Word).SequenceEqual(d.WordTimings.Elements("word").Select(Word))||restored.ReviewCount!=d.ReviewCount)throw new Exception("Processing provenance lost on project round-trip");Console.WriteLine("PASS real media result "+Path.GetFileName(file)+": "+d.Cues.Count+" cues, "+d.Repetitions+", measured word metadata, adjacent SRT export and round-trip");
 }
 Console.WriteLine("TOTAL REAL RESULT "+args.Length+" passed.");
 }
}
