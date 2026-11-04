#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $root 'tests/HardTop.Checks/HardTop.Checks.csproj') -c Release -- --export-demo
if($LASTEXITCODE){throw 'Sample app views could not be exported.'}
& (Join-Path $PSScriptRoot 'demo-scenes.ps1')
python (Join-Path $PSScriptRoot 'render-demo.py')
if($LASTEXITCODE){throw 'Video rendering failed.'}
