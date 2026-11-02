"""Build an optional offline model+runtime update, without changing existing TTS data.

The core updater already verifies every source/ file and supports ZIP64.
This adds the pinned AI files to the same 2.0.14 core payload and splits the
result into SHA-checked parts for download. Rejoining does not install anything.
"""
from pathlib import Path
import hashlib,json,shutil,tempfile,zipfile,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT.parent/'output-2.0.14'
DATA=Path('/tmp/v15-download213')
MODEL_HASH='3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597'
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    core=OUT/'V15VoiceStudio_2.0.14_Core_Update.zip'
    assert core.exists()
    model=DATA/'Qwen3-4B-Instruct-2507-Q4_K_M.gguf'
    assert model.stat().st_size==2497281120 and sha(model)==MODEL_HASH
    assets=json.loads((DATA/'llama-release.json').read_text())
    files={'source/local-ai/model/qwen3-4b.gguf':model}
    for role,folder,archive in [('cpu','win-cpu-x64','llama-b12019-bin-win-cpu-x64.zip'),('gpu','win-vulkan-x64','llama-b12019-bin-win-vulkan-x64.zip')]:
        spec=next(a for a in assets['assets'] if a['name']==archive)
        assert 'sha256:'+sha(DATA/archive)==spec['digest']
        base=DATA/folder
        for file in sorted(base.iterdir()):
            name=file.name
            # Completion is a single owned process; no CLI/server child lifetime.
            if name in ['llama-completion.exe','llama-completion-impl.dll','llama.dll','llama-common.dll','ggml.dll','ggml-base.dll','libomp.dll','ggml-vulkan.dll'] or name.startswith('ggml-cpu-') and name.endswith('.dll') or name.startswith('LICENSE'):
                files[f'source/local-ai/runtime/{role}/{name}']=file
    full=OUT/'V15VoiceStudio_2.0.14_AI_Offline_Update.zip'
    with zipfile.ZipFile(core) as src:
        manifest=ET.fromstring(src.read('manifest.xml'))
        total=sum(int(x.get('size')) for x in manifest)
        for name,p in files.items():
            ET.SubElement(manifest,'file',path=name,size=str(p.stat().st_size),sha256=sha(p))
            total+=p.stat().st_size
        manifest.set('space',str(3*total+128*1024*1024))
        with zipfile.ZipFile(full,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=1,allowZip64=True) as dst:
            dst.writestr('manifest.xml',ET.tostring(manifest,encoding='utf-8',xml_declaration=True))
            for info in src.infolist():
                if info.filename!='manifest.xml':dst.writestr(info,src.read(info))
            for name,p in files.items():dst.write(p,'payload/'+name)
    digest=sha(full)
    limit=420*1024*1024
    parts=[]
    with full.open('rb') as f:
        i=1
        while True:
            data=f.read(limit)
            if not data:break
            part=full.with_name(full.name+f'.{i:03d}')
            part.write_bytes(data);parts.append({'name':part.name,'size':part.stat().st_size,'sha256':sha(part)});i+=1
    metadata={'zip':full.name,'size':full.stat().st_size,'sha256':digest,'parts':parts,'model_sha256':MODEL_HASH,'runtime':'llama.cpp b12019 CPU / Vulkan'}
    (OUT/'ai-offline-package.json').write_text(json.dumps(metadata,indent=2))
    script=r'''@echo off
setlocal
set "V15_AI213_PARTS=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $dir=$env:V15_AI213_PARTS; $name='ZIP_NAME'; $out=[IO.Path]::Combine($dir,$name); $expected='ZIP_HASH'; $length=[int64]ZIP_LENGTH; if(Test-Path -LiteralPath $out){ if((Get-Item -LiteralPath $out).Length -eq $length -and (Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash -eq $expected){ Write-Host 'ZIP da ghep va kiem tra dung. Mo V15 > Cong cu > Cap nhat tu ZIP.'; exit 0 }; throw 'Da co ZIP cung ten khac noi dung. Chuyen file do sang noi khac truoc.' }; $temp=$out+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'; try { $hashes=@(PART_HASHES); $sizes=@(PART_SIZES); $dest=[IO.File]::Create($temp); try { foreach($i in 1..PART_COUNT){ $part=[IO.Path]::Combine($dir,$name+'.'+$i.ToString('000')); if(!(Test-Path -LiteralPath $part)){throw ('Thieu phan '+$i.ToString('000'))}; if((Get-Item -LiteralPath $part).Length -ne $sizes[$i-1]){throw ('Sai kich thuoc phan '+$i.ToString('000'))}; if((Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash -ne $hashes[$i-1]){throw ('Phan bi hong: '+$i.ToString('000'))}; Write-Host ('Dang ghep phan '+$i); $src=[IO.File]::OpenRead($part); try {$src.CopyTo($dest)}finally{$src.Dispose()} } }finally{$dest.Dispose()}; if((Get-Item -LiteralPath $temp).Length -ne $length){throw 'Sai kich thuoc, co phan bi thieu hoac hong.'}; Write-Host 'Dang kiem tra SHA256 ZIP...'; if((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash -ne $expected){throw 'SHA256 khong khop. Tai lai phan bi hong.'}; [IO.File]::Move($temp,$out); Write-Host 'Da ghep xong. Mo V15 > Cong cu va thiet lap > Cap nhat tu ZIP, chon ZIP vua tao.' }finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}}"
if errorlevel 1 echo Khong ghep duoc. Kiem tra cac phan deu o cung thu muc.
pause
endlocal
'''
    script=script.replace('ZIP_NAME',full.name).replace('ZIP_HASH',digest).replace('ZIP_LENGTH',str(full.stat().st_size)).replace('PART_COUNT',str(len(parts))).replace('PART_HASHES',','.join("'"+p['sha256']+"'" for p in parts)).replace('PART_SIZES',','.join(str(p['size']) for p in parts))
    (OUT/'GHEP_AI_2.0.14.cmd').write_bytes(script.replace('\n','\r\n').encode('ascii'))
    print(json.dumps(metadata),flush=True)
if __name__=='__main__':main()
