Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "nsDialogs.nsh"
!ifndef VERSION
 !error "VERSION required"
!endif
!ifndef PAYLOAD
 !error "PAYLOAD required"
!endif
!ifndef OUTPUT
 !error "OUTPUT required"
!endif
!define PRODUCT_KEY "VolturaHardTop"
!define PRODUCT_NAME "Voltura HardTop"
!ifdef DEVELOPMENT
 !undef PRODUCT_KEY
 !define PRODUCT_KEY "VolturaHardTopPreview"
 !undef PRODUCT_NAME
 !define PRODUCT_NAME "Voltura HardTop Preview"
!endif
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}"
Name "Voltura HardTop"
Caption "Voltura HardTop ${VERSION}"
BrandingText "Voltura AB"
OutFile "${OUTPUT}"
!ifdef DEVELOPMENT
 InstallDir "$LOCALAPPDATA\Programs\Voltura HardTop Preview"
!else
 InstallDir "$LOCALAPPDATA\Programs\Voltura HardTop"
!endif
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel user
ManifestDPIAwareness PerMonitorV2
ManifestSupportedOS all
!ifdef ALLOW_SELF_SIGNED
 !uninstfinalize '"${POWERSHELL}" -NoProfile -File "${SIGN_SCRIPT}" -Path "%1" -CertificateThumbprint "${CERT_THUMBPRINT}" -TimestampUrl "${TIMESTAMP_URL}" -AllowSelfSigned' = 0
!else
 !uninstfinalize '"${POWERSHELL}" -NoProfile -File "${SIGN_SCRIPT}" -Path "%1" -CertificateThumbprint "${CERT_THUMBPRINT}" -TimestampUrl "${TIMESTAMP_URL}"' = 0
!endif
SetCompressor /SOLID zlib
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Voltura HardTop"
VIAddVersionKey /LANG=1033 "CompanyName" "Voltura AB"
VIAddVersionKey /LANG=1033 "FileDescription" "Voltura HardTop Installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright ${U+00A9} 2021-2026 Voltura AB"
!ifdef FRAMEWORK_DEPENDENT
VIAddVersionKey /LANG=1033 "OriginalFilename" "VolturaHardTop-Setup-${VERSION}-win-x64-lite.exe"
!else
VIAddVersionKey /LANG=1033 "OriginalFilename" "VolturaHardTop-Setup-${VERSION}-win-x64.exe"
!endif
VIAddVersionKey /LANG=1033 "InternalName" "VolturaHardTopSetup"
VIAddVersionKey /LANG=1033 "Comments" "Developer: Joakim Skoglund; Website: https://hardtop.voltura.se/; Support: https://github.com/voltura/voltura-hardtop/issues; License: MIT; Windows x64; Address: Voltura AB, H${U+00E4}stholmsv${U+00E4}gen 33, SE-131 71 Nacka, Sweden"
!define MUI_ICON "..\src\Voltura.HardTop\Assets\App.ico"
!define MUI_UNICON "..\src\Voltura.HardTop\Assets\App.ico"
!define MUI_ABORTWARNING
!define MUI_LANGDLL_ALLLANGUAGES
!define MUI_LANGDLL_REGISTRY_ROOT HKCU
!define MUI_LANGDLL_REGISTRY_KEY "${UNINSTALL_KEY}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"
!define MUI_WELCOMEFINISHPAGE_BITMAP "assets\wizard.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP_STRETCH FitControl
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "assets\wizard.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP_STRETCH FitControl
!define MUI_PAGE_CUSTOMFUNCTION_SHOW SmoothWelcomeArtwork
!ifdef FRAMEWORK_DEPENDENT
!define MUI_WELCOMEPAGE_TEXT "$(RuntimeNeeded)"
!endif
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\Voltura.HardTop.exe"
!define MUI_PAGE_CUSTOMFUNCTION_SHOW SmoothFinishArtwork
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
UninstPage custom un.Options
!insertmacro MUI_UNPAGE_INSTFILES
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.SmoothFinishArtwork
!insertmacro MUI_UNPAGE_FINISH
!include "languages.nsh"
!include "artwork.nsh"
!include "runtime.nsh"
!insertmacro DefineSmoothArtwork ""
!insertmacro DefineSmoothArtwork "un."
Var SetupMutex
Var RemoveData
Var RemoveControl
Function .onInit
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "$(RequiresX64)"
  Abort
 ${EndIf}
 SetRegView 64
 SetShellVarContext current
 System::Call 'kernel32::CreateMutexW(p 0,i 0,w "Local\${PRODUCT_KEY}-Setup") p.r0 ?e'
 Pop $1
 StrCpy $SetupMutex $0
 ${If} $1 == 183
  Abort
 ${EndIf}
 InitPluginsDir
 File /oname=$PLUGINSDIR\modern-wizard.bmp "assets\wizard.bmp"
 !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd
Section "Voltura HardTop" MainSection
 SectionIn RO
 InitPluginsDir
 SetOutPath "$PLUGINSDIR"
 File "maintenance.ps1"
 !ifdef FRAMEWORK_DEPENDENT
 File "runtime-check.ps1"
 File "runtime-download.ps1"
 StrCpy $PrerequisiteRebootRequired 0
 Call TestWindowsDesktopRuntime
 Pop $0
 ${If} $0 != 0
  Call InstallWindowsDesktopRuntime
 ${EndIf}
 !endif
 nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\maintenance.ps1" -InstallDirectory "$INSTDIR"'
 Pop $0
 Pop $1
 ${If} $0 != 0
  MessageBox MB_ICONSTOP "$(CloseFirst)"
  SetErrorLevel 1
  Abort
 ${EndIf}
 !include "${OBSOLETE_FILES}"
 SetOutPath "$INSTDIR"
 File /r "${PAYLOAD}\*"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 !ifdef FRAMEWORK_DEPENDENT
 WriteRegStr HKCU "${UNINSTALL_KEY}" "PackageVariant" "lite"
 !else
 WriteRegStr HKCU "${UNINSTALL_KEY}" "PackageVariant" "self-contained"
 !endif
 WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Voltura HardTop"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Voltura AB"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallDate" "${INSTALL_DATE}"
 WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" ${INSTALL_SIZE_KB}
 WriteRegStr HKCU "${UNINSTALL_KEY}" "URLUpdateInfo" "https://github.com/voltura/voltura-hardtop/releases/latest"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "Comments" "Keep the windows you need above the rest."
 WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
 WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
 WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\Voltura.HardTop.exe"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "https://hardtop.voltura.se/"
 WriteRegStr HKCU "${UNINSTALL_KEY}" "HelpLink" "https://github.com/voltura/voltura-hardtop/issues"
 WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
 WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
 CreateShortcut "$SMPROGRAMS\${PRODUCT_NAME}.lnk" "$INSTDIR\Voltura.HardTop.exe"
SectionEnd
Section /o "$(StartupLabel)" StartupSection
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${PRODUCT_KEY}" '$\"$INSTDIR\Voltura.HardTop.exe$\" --startup'
SectionEnd
Function un.onInit
 SetRegView 64
 SetShellVarContext current
 InitPluginsDir
 File /oname=$PLUGINSDIR\modern-wizard.bmp "assets\wizard.bmp"
 File /oname=$PLUGINSDIR\maintenance.ps1 "maintenance.ps1"
 !insertmacro MUI_UNGETLANGUAGE
FunctionEnd
Function un.Options
 nsDialogs::Create 1018
 Pop $0
 ${NSD_CreateCheckbox} 0 10u 100% 24u "$(RemoveSettings)"
 Pop $RemoveControl
 ${NSD_OnClick} $RemoveControl un.RemoveChanged
 nsDialogs::Show
FunctionEnd
Function un.RemoveChanged
 ${NSD_GetState} $RemoveControl $RemoveData
FunctionEnd
Section "Uninstall"
 StrCpy $2 ""
 !ifdef DEVELOPMENT
 StrCpy $2 '-DataDirectory "$INSTDIR\Data"'
 !endif
 ${If} $RemoveData == ${BST_CHECKED}
  StrCpy $2 "$2 -RemoveSettings"
 ${EndIf}
 nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\maintenance.ps1" -InstallDirectory "$INSTDIR" $2'
 Pop $0
 Pop $1
 ${If} $0 != 0
  MessageBox MB_ICONSTOP "$(CloseFirst)"
  SetErrorLevel 1
  Abort
 ${EndIf}
 !include "${REMOVE_FILES}"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 Delete "$SMPROGRAMS\${PRODUCT_NAME}.lnk"
 ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${PRODUCT_KEY}"
 ${If} $0 == '$\"$INSTDIR\Voltura.HardTop.exe$\" --startup'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${PRODUCT_KEY}"
 ${EndIf}
 DeleteRegKey HKCU "${UNINSTALL_KEY}"
SectionEnd
