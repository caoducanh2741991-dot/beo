using System;using System.IO;using System.Reflection;
namespace BEO {
public static class BeoBrand {
 public const string Name="BÉO Voice Studio",Version="3.1.0";
 // Installation IDs, cache paths, executable names and mutex stay compatible
 // with existing 3.0/2.x installations. Branding is a display identity.
 public static void UpdateWindowsIdentity(string folder){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
  try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio",true))if(key!=null){key.SetValue("DisplayName",Name);key.SetValue("DisplayVersion",Version);key.SetValue("InstallLocation",folder);}}catch{}
  try{string programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs);if(String.IsNullOrEmpty(programs))return;Type type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return;object shell=Activator.CreateInstance(type);object shortcut=null;try{shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Path.Combine(programs,Name+".lnk")});Type link=shortcut.GetType();link.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{Path.Combine(folder,"V15VoiceStudio.exe")});link.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{folder});link.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{Name});link.InvokeMember("IconLocation",BindingFlags.SetProperty,null,shortcut,new object[]{Path.Combine(folder,"V15VoiceStudio.exe")+",0"});link.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);}finally{if(shortcut!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}}catch{}
 }
}
}
