using System;using V15Update;
public static class FreshUpdaterRunner {
 public static int Main(string[] args){try{if(args.Length!=2)throw new ArgumentException("Expected package and installation folder");UpdateEngine.Apply(args[0],args[1],Console.WriteLine);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
