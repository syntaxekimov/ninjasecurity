; Ninja Security — NSIS Installer Script
; Build: makensis installer/ninja-security.nsi
; Requires: NSIS 3.x, publish artifacts in publish\

!define APP_NAME      "Ninja Security"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "Ninja Security"
!define SERVICE_NAME  "NinjaSecurity"
!define REG_UNINSTALL "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "NinjaSecuritySetup-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma

;--- Pages ---
!include "MUI2.nsh"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

;--- Install ---
Section "Main" SecMain
  SectionIn RO

  SetOutPath "$INSTDIR\service"
  File /r "publish\service\*.*"

  SetOutPath "$INSTDIR\app"
  File /r "publish\app\*.*"

  ; Register Windows service
  ExecWait '"$SYSDIR\sc.exe" create "${SERVICE_NAME}" \
    binPath= "$INSTDIR\service\NinjaSecurity.Service.exe" \
    DisplayName= "${APP_NAME} Service" \
    start= auto' $0

  ExecWait '"$SYSDIR\sc.exe" description "${SERVICE_NAME}" \
    "Ninja Security antivirus engine — real-time protection, scanning, quarantine"' $1

  ; Start the service
  ExecWait '"$SYSDIR\sc.exe" start "${SERVICE_NAME}"' $2

  ; Start Menu shortcut
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" \
    "$INSTDIR\app\NinjaSecurity.App.exe"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Удалить ${APP_NAME}.lnk" \
    "$INSTDIR\Uninstall.exe"

  ; Desktop shortcut
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" \
    "$INSTDIR\app\NinjaSecurity.App.exe"

  ; App auto-start with Windows (optional — launch GUI on login)
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" \
    "${APP_NAME}" '"$INSTDIR\app\NinjaSecurity.App.exe"'

  ; Store install dir + version
  WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\${APP_NAME}" "Version"    "${APP_VERSION}"

  ; Uninstaller registration
  WriteRegStr   HKLM "${REG_UNINSTALL}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKLM "${REG_UNINSTALL}" "DisplayVersion"  "${APP_VERSION}"
  WriteRegStr   HKLM "${REG_UNINSTALL}" "Publisher"       "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${REG_UNINSTALL}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${REG_UNINSTALL}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${REG_UNINSTALL}" "NoModify" 1
  WriteRegDWORD HKLM "${REG_UNINSTALL}" "NoRepair" 1

  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

;--- Uninstall ---
Section "Uninstall"
  ; Stop and remove service
  ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
  ExecWait '"$SYSDIR\sc.exe" delete "${SERVICE_NAME}"'

  ; Remove shortcuts
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Удалить ${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  ; Remove auto-start entry
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"

  ; Remove files
  RMDir /r "$INSTDIR\service"
  RMDir /r "$INSTDIR\app"
  Delete    "$INSTDIR\Uninstall.exe"
  RMDir     "$INSTDIR"

  ; Remove registry
  DeleteRegKey HKLM "Software\${APP_NAME}"
  DeleteRegKey HKLM "${REG_UNINSTALL}"
SectionEnd
