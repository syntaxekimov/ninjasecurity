; Ninja Security — NSIS Installer
; Build manually : makensis installer/ninja-security.nsi
; Build with ver : makensis /DAPP_VERSION=1.2.3 installer/ninja-security.nsi
; CI builds this automatically via .github/workflows/release.yml

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif

!define APP_NAME      "Ninja Security"
!define APP_PUBLISHER "Ninja Security"
!define SERVICE_NAME  "NinjaSecurity"
!define INSTALL_KEY   "Software\${APP_NAME}"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define AUTORUN_KEY   "Software\Microsoft\Windows\CurrentVersion\Run"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "..\NinjaSecuritySetup-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${INSTALL_KEY}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Unicode true

;--- Modern UI ---
!include "MUI2.nsh"
!include "LogicLib.nsh"

!define MUI_ABORTWARNING
; Icon is optional — generated from SVG by CI; local builds work without it
!if /FileExists "..\assets\ninja-security.ico"
  !define MUI_ICON   "..\assets\ninja-security.ico"
  !define MUI_UNICON "..\assets\ninja-security.ico"
!endif

; Welcome page
!define MUI_WELCOMEPAGE_TITLE    "Добро пожаловать в ${APP_NAME} ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT     "Мастер установит ${APP_NAME} на ваш компьютер.$\r$\n$\r$\nАнтивирус использует ClamAV, YARA и базу хешей для обнаружения угроз. Защита в реальном времени запускается как Windows-служба.$\r$\n$\r$\nНажмите Далее для продолжения."

; Finish page
!define MUI_FINISHPAGE_TITLE     "Установка завершена"
!define MUI_FINISHPAGE_TEXT      "${APP_NAME} установлен и защита активна.$\r$\n$\r$\nСлужба запущена автоматически."
!define MUI_FINISHPAGE_RUN       "$INSTDIR\app\NinjaSecurity.App.exe"
!define MUI_FINISHPAGE_RUN_TEXT  "Запустить ${APP_NAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Russian"

;─────────────────────────────────────────────────────────────────────────────
; Helpers
;─────────────────────────────────────────────────────────────────────────────

; Stop + remove existing service gracefully before (re)install
!macro StopAndRemoveService
  ; Ignore errors — service may not exist yet
  ClearErrors
  ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
  Sleep 2000
  ExecWait '"$SYSDIR\sc.exe" delete "${SERVICE_NAME}"'
  Sleep 1000
!macroend

;─────────────────────────────────────────────────────────────────────────────
; Install
;─────────────────────────────────────────────────────────────────────────────
Section "Main" SecMain
  SectionIn RO

  ; Stop any previous version's service before overwriting files
  !insertmacro StopAndRemoveService

  ; Copy service binaries
  SetOutPath "$INSTDIR\service"
  File /r "..\publish\service\*.*"

  ; Copy GUI binaries
  SetOutPath "$INSTDIR\app"
  File /r "..\publish\app\*.*"

  ; Register Windows service (auto-start)
  ExecWait '"$SYSDIR\sc.exe" create "${SERVICE_NAME}" \
    binPath= "$INSTDIR\service\NinjaSecurity.Service.exe" \
    DisplayName= "${APP_NAME} Service" \
    start= auto'

  ExecWait '"$SYSDIR\sc.exe" description "${SERVICE_NAME}" \
    "Ninja Security — real-time antivirus protection, scanning and quarantine"'

  ; Start the service immediately
  ExecWait '"$SYSDIR\sc.exe" start "${SERVICE_NAME}"'

  ; Start Menu
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" \
    "$INSTDIR\app\NinjaSecurity.App.exe" "" \
    "$INSTDIR\app\NinjaSecurity.App.exe" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Удалить ${APP_NAME}.lnk" \
    "$INSTDIR\Uninstall.exe"

  ; Desktop shortcut
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" \
    "$INSTDIR\app\NinjaSecurity.App.exe" "" \
    "$INSTDIR\app\NinjaSecurity.App.exe" 0

  ; Auto-start GUI with Windows login
  WriteRegStr HKCU "${AUTORUN_KEY}" "${APP_NAME}" \
    '"$INSTDIR\app\NinjaSecurity.App.exe"'

  ; Store install metadata
  WriteRegStr   HKLM "${INSTALL_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr   HKLM "${INSTALL_KEY}" "Version"    "${APP_VERSION}"

  ; Add/Programs uninstall entry
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayName"          "${APP_NAME}"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayVersion"       "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "Publisher"            "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "InstallLocation"      "$INSTDIR"
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "UninstallString"      '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayIcon"          "$INSTDIR\app\NinjaSecurity.App.exe,0"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify"             1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair"             1

  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

;─────────────────────────────────────────────────────────────────────────────
; Uninstall
;─────────────────────────────────────────────────────────────────────────────
Section "Uninstall"
  ; Stop and remove service
  ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
  Sleep 2000
  ExecWait '"$SYSDIR\sc.exe" delete "${SERVICE_NAME}"'
  Sleep 500

  ; Remove shortcuts
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Удалить ${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  ; Remove GUI auto-start
  DeleteRegValue HKCU "${AUTORUN_KEY}" "${APP_NAME}"

  ; Remove files (keep user data in AppData)
  RMDir /r "$INSTDIR\service"
  RMDir /r "$INSTDIR\app"
  Delete    "$INSTDIR\Uninstall.exe"
  RMDir     "$INSTDIR"

  ; Remove registry
  DeleteRegKey HKLM "${INSTALL_KEY}"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
