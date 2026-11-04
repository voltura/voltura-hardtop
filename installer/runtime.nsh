; Adapted from Voltura Air. Only the Desktop runtime is needed by HardTop.
!ifdef FRAMEWORK_DEPENDENT
Var PrerequisiteRebootRequired
!macro ExecCheckedToStack COMMAND_VAR TOO_LONG_LABEL
  StrLen $3 ${COMMAND_VAR}
  IntCmp $3 ${NSIS_MAX_STRLEN} ${TOO_LONG_LABEL} 0 ${TOO_LONG_LABEL}
  nsExec::ExecToStack '${COMMAND_VAR}'
!macroend

!if ${NSIS_PTR_SIZE} > 4
  !define /math VOLTURA_SHELLEXECUTEINFO_SIZE 14 * ${NSIS_PTR_SIZE}
!else
  !define VOLTURA_SHELLEXECUTEINFO_SIZE 60
!endif

!macro ExecElevatedAndWait FILE_PATH PARAMETERS RESULT_VAR
  System::Store S
  System::Call '*(&i${VOLTURA_SHELLEXECUTEINFO_SIZE})p.r0'
  System::Call '*$0(i ${VOLTURA_SHELLEXECUTEINFO_SIZE}, i 0x40, p $HWNDPARENT, t "runas", t "${FILE_PATH}", t "${PARAMETERS}", t "$PLUGINSDIR", i ${SW_HIDE})p.r0'
  System::Call 'shell32::ShellExecuteEx(t)(p r0)i.r1 ?e'
  Pop $2
  ${If} $1 != 0
    System::Call '*$0(is, i, p, p, p, p, p, p, p, p, p, p, p, p, p.r1)'
    System::Call 'kernel32::WaitForSingleObject(p r1, i -1)i.r2'
    ${If} $2 == 0
      System::Call 'kernel32::GetExitCodeProcess(p r1, *i.r2)i.r3'
      ${If} $3 != 0
        Push $2
      ${Else}
        Push 51002
      ${EndIf}
    ${Else}
      Push 51002
    ${EndIf}
    System::Call 'kernel32::CloseHandle(p r1)'
  ${ElseIf} $2 == 1223
    Push 51001
  ${Else}
    Push 51002
  ${EndIf}
  System::Free $0
  System::Store L
  Pop ${RESULT_VAR}
!macroend



Function TestWindowsDesktopRuntime
  DetailPrint "Checking for the .NET 10 Windows Desktop runtime..."
  StrCpy $2 '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\runtime-check.ps1" -MinimumVersion "${MINIMUM_RUNTIME}"'
  !insertmacro ExecCheckedToStack $2 windows_desktop_detection_too_long
  Pop $0
  Pop $1
  Push $0
  Return

windows_desktop_detection_too_long:
  DetailPrint "Controlled failure: the Windows Desktop detection command exceeded the NSIS command capacity."
  MessageBox MB_ICONSTOP "$(RuntimeFailed)"
  SetErrorLevel 1
  Abort "The Windows Desktop runtime command was too long."
FunctionEnd

Function InstallWindowsDesktopRuntime
  DetailPrint "Downloading the .NET 10 Windows Desktop runtime..."
  StrCpy $2 '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\runtime-download.ps1" -Destination "$PLUGINSDIR\VolturaHardTop-WindowsDesktop.exe"'
  !insertmacro ExecCheckedToStack $2 windows_desktop_internal_failure
  Pop $0
  Pop $1
  ${If} $0 != 0
    Call CleanupWindowsDesktopRuntime
    DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime download failed or was empty."
    MessageBox MB_ICONSTOP "$(RuntimeFailed)"
    SetErrorLevel 1
    Abort "The Windows Desktop runtime download failed."
  ${EndIf}

  DetailPrint "Verifying the .NET 10 Windows Desktop runtime signature and Microsoft signer..."
  StrCpy $2 '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\runtime-download.ps1" -Destination "$PLUGINSDIR\VolturaHardTop-WindowsDesktop.exe" -VerifyOnly'
  !insertmacro ExecCheckedToStack $2 windows_desktop_internal_failure
  Pop $0
  Pop $1
  ${If} $0 != 0
    StrCpy $4 $0
    Call CleanupWindowsDesktopRuntime
    ${If} $4 == 21
      DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime signature was not valid."
      MessageBox MB_ICONSTOP "$(RuntimeFailed)"
    ${ElseIf} $4 == 22
      DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime had no signer certificate."
      MessageBox MB_ICONSTOP "$(RuntimeFailed)"
    ${Else}
      DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime signer was not Microsoft Corporation."
      MessageBox MB_ICONSTOP "$(RuntimeFailed)"
    ${EndIf}
    SetErrorLevel 1
    Abort "The Windows Desktop runtime verification failed."
  ${EndIf}

  DetailPrint "Requesting elevation for the .NET 10 Windows Desktop runtime..."
  DetailPrint "Installing the .NET 10 Windows Desktop runtime..."
  SetDetailsPrint listonly
  DetailPrint "The Microsoft runtime installer can take several minutes."
  DetailPrint "Voltura HardTop setup continues automatically when it finishes."
  SetDetailsPrint both
  !insertmacro ExecElevatedAndWait "$PLUGINSDIR\VolturaHardTop-WindowsDesktop.exe" "/install /quiet /norestart" $0
  StrCpy $4 $0
  Call CleanupWindowsDesktopRuntime
  StrCpy $0 $4

  ${If} $0 == 0
    DetailPrint "Validating the .NET 10 Windows Desktop runtime..."
    Call TestWindowsDesktopRuntime
    Pop $0
    ${If} $0 != 0
      DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime was absent after a successful child installer result."
      MessageBox MB_ICONSTOP "$(RuntimeFailed)"
      SetErrorLevel 1
    Abort "The Windows Desktop runtime validation failed."
    ${EndIf}
    Return
  ${EndIf}

  ${If} $0 == 3010
    DetailPrint "The .NET 10 Windows Desktop runtime requires a restart."
    StrCpy $PrerequisiteRebootRequired 1
    SetRebootFlag true
    DetailPrint "Validating the .NET 10 Windows Desktop runtime..."
    Call TestWindowsDesktopRuntime
    Pop $0
    ${If} $0 != 0
      DetailPrint "The .NET 10 Windows Desktop runtime is provisionally complete pending restart."
    ${EndIf}
    Return
  ${EndIf}

  ${If} $0 == 51001
    DetailPrint "Controlled failure: elevation was denied for the .NET 10 Windows Desktop runtime."
    MessageBox MB_ICONSTOP "$(RuntimeFailed)"
  ${ElseIf} $0 == 51002
    DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime process could not be started."
    MessageBox MB_ICONSTOP "$(RuntimeFailed)"
  ${Else}
    DetailPrint "Controlled failure: the .NET 10 Windows Desktop runtime installer returned exit code $0."
    MessageBox MB_ICONSTOP "$(RuntimeFailed)"
  ${EndIf}
  SetErrorLevel 1
  Abort "The Windows Desktop runtime installation failed."

windows_desktop_internal_failure:
  Call CleanupWindowsDesktopRuntime
  DetailPrint "Controlled failure: a Windows Desktop prerequisite command exceeded the NSIS command capacity."
  MessageBox MB_ICONSTOP "$(RuntimeFailed)"
  SetErrorLevel 1
  Abort "The Windows Desktop runtime command was too long."
FunctionEnd

Function CleanupWindowsDesktopRuntime
  DetailPrint "Cleaning up the .NET 10 Windows Desktop runtime installer."
  Delete "$PLUGINSDIR\VolturaHardTop-WindowsDesktop.exe"
FunctionEnd
!endif
