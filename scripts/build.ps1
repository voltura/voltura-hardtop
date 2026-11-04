#Requires -Version 7.0
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts/publish'}
dotnet publish (Join-Path $root 'src/Voltura.HardTop/Voltura.HardTop.csproj') -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o $OutputDirectory -p:DebugType=None -p:DebugSymbols=false
if($LASTEXITCODE){throw 'Release build failed.'}
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
& (Join-Path $PSScriptRoot 'set-exe-metadata.ps1') -Path (Join-Path $OutputDirectory 'Voltura.HardTop.exe') -Version $version
$commit=(& git -C $root rev-parse --verify HEAD 2>$null | Out-String).Trim()
if($LASTEXITCODE){$commit=$null}
$dirty=((& git -C $root status --porcelain | Out-String).Trim()).Length -gt 0
@{sourceCommit=$commit;sourceClean=!$dirty;version=$version;architecture='win-x64';sdk=(& dotnet --version)} |
    ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'build-provenance.json') -Encoding utf8
Write-Host "Built self-contained Windows x64 payload: $OutputDirectory"
