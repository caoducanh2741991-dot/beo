using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;
public static class FrameworkCompatibilityTests {
 static readonly Dictionary<short,OpCode> codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
 static int UnsupportedStringCalls(string path){var a=Assembly.LoadFile(Path.GetFullPath(path));int hits=0;
  foreach(var type in a.GetTypes())foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))){
   var body=method.GetMethodBody();if(body==null)continue;var il=body.GetILAsByteArray();int i=0;
   while(i<il.Length){short value=il[i++];if(value==0xfe)value=(short)(0xfe00|il[i++]);var op=codes[value];int length=0;
    switch(op.OperandType){case OperandType.InlineNone:break;case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:length=1;break;case OperandType.InlineVar:length=2;break;case OperandType.InlineI8:case OperandType.InlineR:length=8;break;case OperandType.InlineSwitch:length=4+4*BitConverter.ToInt32(il,i);break;default:length=4;break;}
    if(op.OperandType==OperandType.InlineMethod){var called=method.Module.ResolveMethod(BitConverter.ToInt32(il,i));if(called.DeclaringType==typeof(string)&&called.Name=="Contains"&&called.GetParameters().Length==1&&called.GetParameters()[0].ParameterType==typeof(char))hits++;}
    i+=length;
   }
  }return hits;
 }
 public static void Main(string[] args){string file=args[0]=="--scan"?args[1]:args[0];var assembly=Assembly.LoadFile(Path.GetFullPath(file));Console.WriteLine("VERSION="+assembly.GetName().Version+" UNSUPPORTED="+UnsupportedStringCalls(file));}
}
