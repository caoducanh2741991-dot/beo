@echo off
setlocal
set "V15_UPDATE_STAGE=%TEMP%\V15_update_%RANDOM%_%RANDOM%"
mkdir "%V15_UPDATE_STAGE%"
copy /y "%~dp0V15Updater.exe" "%V15_UPDATE_STAGE%\V15Updater.exe" >nul
if errorlevel 1 exit /b 1
start "V15 Update" "%V15_UPDATE_STAGE%\V15Updater.exe" --folder "%~dp0."
