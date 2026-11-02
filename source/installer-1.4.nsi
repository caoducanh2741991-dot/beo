Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
Name "V15 Voice Studio Offline 1.4.0"
OutFile "/tmp/v15-release/V15VoiceStudio_Offline_Setup.exe"
InstallDir "$LOCALAPPDATA\V15VoiceStudio"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "V15 Voice Studio · Offline 1.4.0"
Var Stage
Var InCommit
!define MUI_WELCOMEPAGE_TITLE "V15 Voice Studio 1.4.0"
!define MUI_WELCOMEPAGE_TEXT "Thêm Qwen3-TTS, Chatterbox Nano và sao chép giọng MP3/WAV. Có lưu hàng đợi, so sánh giọng và bộ thiết lập phong cách.$\r$\n$\r$\nTải đủ mọi phần V15VoiceStudio_1.4_Data.7z.001, .002... vào cùng thư mục với bộ cài này. Không đổi tên các phần.$\r$\n$\r$\nĐóng V15 Voice Studio trước khi tiếp tục. Bộ cài dùng cho cả cài mới và cập nhật."
!define MUI_CUSTOMFUNCTION_ABORT UserAbortGuard
!define MUI_FINISHPAGE_RUN "$INSTDIR\V15VoiceStudio.exe"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Vietnamese"
Function .onInit
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "Cần Windows 10/11 64-bit."
  Abort
 ${EndIf}
 ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
 ${If} $0 < 378389
  MessageBox MB_ICONSTOP "Cần .NET Framework 4.5 trở lên. Windows 10/11 thường đã có sẵn."
  Abort
 ${EndIf}
 ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "InstallLocation"
 StrCmp $0 "" find_old found
 find_old:
 ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "DisplayIcon"
 StrCmp $0 "" init_done
 ${GetParent} "$0" $0
 found:
 IfFileExists "$0\V15VoiceStudio.exe" 0 init_done
 StrCpy $INSTDIR $0
 init_done:
FunctionEnd
Function RequireClosed
 check_file:
 IfFileExists "$INSTDIR\V15VoiceStudio.exe" 0 ready
 System::Call 'kernel32::CreateFileW(w "$INSTDIR\V15VoiceStudio.exe", i 0xC0000000, i 0, p 0, i 3, i 0x80, p 0) p.r0'
 StrCmp $0 -1 locked
 System::Call 'kernel32::CloseHandle(p r0)'
 Goto ready
 locked:
 MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Hãy đóng V15 Voice Studio rồi chọn Thử lại. Nếu app vẫn chạy, dừng tác vụ và đóng app trước.$\r$\n$\r$\nNếu app đã đóng, kiểm tra quyền ghi thư mục cài đặt." IDRETRY check_file
 Abort
 ready:
FunctionEnd
Function UserAbortGuard
 StrCmp $InCommit "1" 0 allow_abort
 Abort
 allow_abort:
FunctionEnd
Section "V15 Voice Studio 1.4.0"
 Call RequireClosed
 InitPluginsDir
 SetOutPath "$PLUGINSDIR"
 File "/tmp/v15-build/InstallTools.exe"
 File "/tmp/v15-bootstrap/7zr.exe"
 File "/tmp/v15-build/parts.sha256"
 File "/tmp/v15-build/install-files.sha256"
 DetailPrint "Kiểm tra đủ các phần dữ liệu và mã SHA-256..."
 nsExec::ExecToStack '"$PLUGINSDIR\InstallTools.exe" verify "$EXEDIR" "$PLUGINSDIR\parts.sha256"'
 Pop $0
 Pop $1
 StrCmp $0 "0" parts_ok
 MessageBox MB_ICONSTOP "Thiếu phần dữ liệu hoặc tệp tải về bị lỗi. Hãy tải đủ các phần vào cùng thư mục với bộ cài.$\r$\n$\r$\n$1"
 Abort
 parts_ok:
 CreateDirectory "$INSTDIR"
 nsExec::ExecToStack '"$PLUGINSDIR\InstallTools.exe" space "$INSTDIR" "$PLUGINSDIR\install-files.sha256"'
 Pop $0
 Pop $1
 StrCmp $0 "0" space_ok
 MessageBox MB_ICONSTOP "$1"
 Abort
 space_ok:
 GetTempFileName $Stage "$INSTDIR"
 Delete "$Stage"
 CreateDirectory "$Stage"
 DetailPrint "Giải nén dữ liệu offline. Quá trình này có thể mất vài phút..."
 nsExec::ExecToStack '"$PLUGINSDIR\7zr.exe" x "$EXEDIR\V15VoiceStudio_1.4_Data.7z.001" -o"$Stage" -y -bso0 -bsp0'
 Pop $0
 Pop $1
 StrCmp $0 "0" extract_ok
 RMDir /r "$Stage"
 MessageBox MB_ICONSTOP "Không giải nén được dữ liệu. Bản đang cài được giữ nguyên.$\r$\n$1"
 Abort
 extract_ok:
 SetOutPath "$Stage"
 File /r "/tmp/v15-build/core/*"
 DetailPrint "Kiểm tra từng tệp trước khi cập nhật..."
 nsExec::ExecToStack '"$PLUGINSDIR\InstallTools.exe" verify "$Stage" "$PLUGINSDIR\install-files.sha256"'
 Pop $0
 Pop $1
 StrCmp $0 "0" verified
 SetOutPath "$TEMP"
 RMDir /r "$Stage"
 MessageBox MB_ICONSTOP "Dữ liệu chưa hợp lệ. Bản đang cài được giữ nguyên.$\r$\n$1"
 Abort
 verified:
 Call RequireClosed
 StrCpy $InCommit "1"
 GetDlgItem $0 $HWNDPARENT 2
 EnableWindow $0 0
 SetOutPath "$INSTDIR"
 DetailPrint "Cài bản 1.4.0. Vui lòng chờ hoàn tất..."
 nsExec::ExecToStack '"$PLUGINSDIR\InstallTools.exe" commit "$Stage" "$INSTDIR" "$PLUGINSDIR\install-files.sha256"'
 Pop $0
 Pop $1
 StrCpy $InCommit "0"
 GetDlgItem $2 $HWNDPARENT 2
 EnableWindow $2 1
 StrCmp $0 "0" committed
 MessageBox MB_ICONSTOP "Chưa cập nhật xong. Xem thông tin khôi phục bên dưới.$\r$\n$1"
 Abort
 committed:
 RMDir /r "$Stage"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 CreateDirectory "$SMPROGRAMS\V15 Voice Studio"
 CreateShortcut "$SMPROGRAMS\V15 Voice Studio\V15 Voice Studio.lnk" "$INSTDIR\V15VoiceStudio.exe"
 CreateShortcut "$SMPROGRAMS\V15 Voice Studio\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
 CreateShortcut "$DESKTOP\V15 Voice Studio.lnk" "$INSTDIR\V15VoiceStudio.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "DisplayName" "V15 Voice Studio Offline"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "DisplayVersion" "1.4.0"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "DisplayIcon" "$INSTDIR\V15VoiceStudio.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "UninstallString" '"$INSTDIR\Uninstall.exe"'
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "NoModify" 1
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio" "NoRepair" 1
 DetailPrint "Hoàn tất. Kịch bản, WAV và các ghi chú riêng được giữ nguyên."
SectionEnd
Section "Uninstall"
 Delete "$DESKTOP\V15 Voice Studio.lnk"
 Delete "$SMPROGRAMS\V15 Voice Studio\V15 Voice Studio.lnk"
 Delete "$SMPROGRAMS\V15 Voice Studio\Uninstall.lnk"
 RMDir "$SMPROGRAMS\V15 Voice Studio"
 ; Delete only release-owned files; keep scripts or WAVs the user placed here.
 !include "/tmp/v15-build/uninstall-files.nsh"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\V15VoiceStudio"
SectionEnd
