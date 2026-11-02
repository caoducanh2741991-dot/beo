@echo off
setlocal
set "V15ROOT=%~dp0..\..\.."
if not exist "%V15ROOT%\ai\runtime\python.exe" (
 echo Thieu Python cua goi AI. Hay cai du bo AI truoc.
 pause
 exit /b 2
)
"%V15ROOT%\ai\runtime\python.exe" "%~dp0setup_vi.py" --folder "%LOCALAPPDATA%\V15VoiceStudio\vietnamese_pack" --mode gpu --ffmpeg "%V15ROOT%\source\media\ffmpeg.exe"
if errorlevel 1 echo Cai chua hoan tat. Mo app, chon Cai goi Viet de thu lai.
pause
