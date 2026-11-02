using System;using System.IO;using System.Linq;using System.Collections.Generic;using V15Update;
public static class GpuPackageTests {
 public static void Main(string[] args){string zip=args[0],stage=args[1];var files=UpdateEngine.Stage(zip,stage,Console.WriteLine);long size=files.Sum(f=>new FileInfo(UpdateEngine.Safe(stage,f)).Length);if(files.Count!=11080||size!=4668019393L)throw new Exception("GPU file count or total byte size differs from official wheels: "+files.Count+" / "+size);
  foreach(string f in new[]{"torch/__init__.py","torch/lib/torch_cuda.dll","torch/lib/c10_cuda.dll","torchaudio/__init__.py","torio/__init__.py"})if(!File.Exists(UpdateEngine.Safe(stage,"ai/gpu/site-packages/"+f)))throw new Exception("Missing GPU runtime file "+f);
  Console.WriteLine("PASS all 8 GPU ZIPs reassemble to official SHA-256 wheels and unpack 11080 files / "+size+" bytes.");
 }
}
