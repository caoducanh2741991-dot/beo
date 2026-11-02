using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
public static class InstallTools {
 sealed class Entry {public string Hash,Name;public long Size;}
 static IEnumerable<Entry> Manifest(string file){foreach(string line in File.ReadLines(file)){if(String.IsNullOrWhiteSpace(line))continue;var c=line.Split('\t');long size;if(c.Length!=3||c[0].Length!=64||!long.TryParse(c[1],out size)||size<0)throw new Exception("Invalid installation manifest");yield return new Entry{Hash=c[0],Size=size,Name=c[2]};}}
 static string Resolve(string root,string relative){string dir=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,path=Path.GetFullPath(Path.Combine(dir,relative.Replace('/',Path.DirectorySeparatorChar)));if(!path.StartsWith(dir,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe installation path");return path;}
 static void Verify(string root,string manifest){foreach(var e in Manifest(manifest)){string file=Resolve(root,e.Name);if(!File.Exists(file)||new FileInfo(file).Length!=e.Size)throw new Exception("Missing or incomplete file: "+e.Name);using(var hash=SHA256.Create())using(var stream=File.OpenRead(file)){if(!BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").Equals(e.Hash,StringComparison.OrdinalIgnoreCase))throw new Exception("Checksum mismatch: "+e.Name);}}}
 static void Commit(string stage,string target,string manifest){string backup=Path.Combine(target,".v15_140_backup_"+Guid.NewGuid().ToString("N"));var changes=new List<Tuple<string,string>>();Directory.CreateDirectory(backup);bool success=false;
  try{foreach(var e in Manifest(manifest)){string source=Resolve(stage,e.Name),dest=Resolve(target,e.Name),old=Resolve(backup,e.Name);Directory.CreateDirectory(Path.GetDirectoryName(dest));if(File.Exists(dest)){Directory.CreateDirectory(Path.GetDirectoryName(old));File.Move(dest,old);changes.Add(Tuple.Create(dest,old));}else changes.Add(Tuple.Create(dest,""));File.Move(source,dest);}success=true;}
  catch(Exception error){bool restored=true;foreach(var change in changes.AsEnumerable().Reverse()){try{if(File.Exists(change.Item1))File.Delete(change.Item1);if(change.Item2.Length>0&&File.Exists(change.Item2))File.Move(change.Item2,change.Item1);}catch{restored=false;}}
   if(!restored)throw new Exception("Installation stopped. Recovery files were preserved at "+backup+". "+error.Message);throw new Exception("Installation stopped; previous files restored. "+error.Message);
  }finally{if(success){try{Directory.Delete(backup,true);}catch{}}}
 }
 public static int Main(string[] args){try{
  if(args.Length<3)throw new Exception("Missing installer arguments");
  if(args[0]=="verify")Verify(args[1],args[2]);
  else if(args[0]=="space"){long need=Manifest(args[2]).Sum(e=>e.Size)+512L*1024*1024;var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(args[1])));if(drive.AvailableFreeSpace<need)throw new Exception("Need at least "+Math.Ceiling(need/1073741824.0)+" GB free on the installation drive.");}
  else if(args[0]=="commit"&&args.Length==4)Commit(args[1],args[2],args[3]);
  else throw new Exception("Unknown installer command");Console.WriteLine("OK");return 0;
 }catch(Exception e){Console.WriteLine(e.Message);return 2;}}
}
