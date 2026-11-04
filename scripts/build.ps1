#Requires -Version 7.0
param([string]$OutputDirectory, [switch]$FrameworkDependent)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$commit=(& git -C $root rev-parse --verify HEAD | Out-String).Trim()
$variant=if($FrameworkDependent){'lite'}else{'self-contained'}
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
if(!$OutputDirectory){$OutputDirectory=Join-Path $root "artifacts/builds/$version/$commit/$variant"}
# Refuse mixed/stale payloads rather than deleting a caller-selected directory.
if((Test-Path $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Choose an empty output directory for a new build.'}
$selfContained=(!$FrameworkDependent).ToString().ToLowerInvariant()
dotnet publish (Join-Path $root 'src/Voltura.HardTop/Voltura.HardTop.csproj') -c Release -r win-x64 --self-contained $selfContained -p:RestoreLockedMode=true -o $OutputDirectory -p:DebugType=None -p:DebugSymbols=false
if($LASTEXITCODE){throw 'Release build failed.'}
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
& (Join-Path $PSScriptRoot 'set-exe-metadata.ps1') -Path (Join-Path $OutputDirectory 'Voltura.HardTop.exe') -Version $version
$commit=(& git -C $root rev-parse --verify HEAD 2>$null | Out-String).Trim()
if($LASTEXITCODE){$commit=$null}
$dirty=((& git -C $root status --porcelain | Out-String).Trim()).Length -gt 0
@{sourceCommit=$commit;sourceClean=!$dirty;version=$version;architecture='win-x64';selfContained=!$FrameworkDependent;sdk=(& dotnet --version)} |
    ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'build-provenance.json') -Encoding utf8
Write-Host "Built Windows x64 payload (self-contained: $selfContained): $OutputDirectory"
