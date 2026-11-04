#Requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../installer/runtime-check.ps1')
function Assert([bool]$Condition,[string]$Message){if(!$Condition){throw $Message}}
function Runtimes([string]$Core,[string]$Desktop){
    @("Microsoft.NETCore.App $Core [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]",
      "Microsoft.WindowsDesktop.App $Desktop [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]")
}
Assert (Test-DesktopRuntime (Runtimes '10.0.14' '10.0.14') '10.0.0') 'Compatible patches should be accepted.'
Assert (!(Test-DesktopRuntime @() '10.0.0')) 'Missing runtimes must trigger bootstrap.'
Assert (!(Test-DesktopRuntime (Runtimes '9.0.14' '9.0.14') '10.0.0')) 'An old major is not sufficient.'
Assert (!(Test-DesktopRuntime (Runtimes '11.0.0' '11.0.0') '10.0.0')) 'A newer major does not satisfy default roll-forward.'
Assert (!(Test-DesktopRuntime (Runtimes '10.0.0' '10.0.0-preview.1') '10.0.0')) 'Preview runtime must not satisfy a stable app.'
Assert (!(Test-DesktopRuntime (Runtimes '10.0.14' '10.0.13') '10.0.14')) 'A patch below the application minimum is insufficient.'
Assert (!(Test-DesktopRuntime @('Microsoft.NETCore.App 10.0.14 [C:\dotnet]') '10.0.0')) 'Core runtime alone cannot run WPF.'
Assert (!(Test-DesktopRuntime @('Microsoft.WindowsDesktop.App 10.0.14 [C:\dotnet]') '10.0.0')) 'Desktop metadata without Core is incomplete.'
Assert (Test-DesktopRuntime ((Runtimes '9.0.14' '9.0.14')+(Runtimes '10.0.14' '10.0.14')) '10.0.0') 'Side-by-side runtime discovery failed.'
Write-Host 'Nine runtime prerequisite checks passed.'
