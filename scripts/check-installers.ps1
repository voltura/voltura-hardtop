#Requires -Version 7.0
param([Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$report=Get-Content (Join-Path $ReleaseDirectory 'release-manifest.json') -Raw|ConvertFrom-Json
if(!$report.development -or $report.packageMode -ne 'preview'){throw 'Only isolated preview installers may be used by this check.'}
$previewKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\VolturaHardTopPreview'
if(Test-Path $previewKey){throw 'An existing preview installation must be preserved. Uninstall it explicitly before running this check.'}
$install=Join-Path $root ('artifacts/checks/installers-'+[guid]::NewGuid().ToString('N'))
$runKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$programs=[Environment]::GetFolderPath('Programs')
$productionLink=Join-Path $programs 'Voltura HardTop.lnk'
$settings=Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/settings.json'
function Fingerprint([string]$Path){if(Test-Path -LiteralPath $Path){return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash};return '<absent>'}
function StartupEntry {return (Get-ItemProperty -LiteralPath $runKey -Name VolturaHardTop -ErrorAction SilentlyContinue).VolturaHardTop}
$beforeLink=Fingerprint $productionLink
$beforeSettings=Fingerprint $settings
$beforeStartup=StartupEntry
$measurements=@()
foreach($variant in 'self-contained','lite','self-contained'){
    $suffix=if($variant -eq 'lite'){'-lite'}else{''}
    $name="VolturaHardTop-Setup-$($report.version)-win-x64$suffix.exe"
    $asset=@($report.assets|Where-Object name -eq $name)
    $path=Join-Path $ReleaseDirectory $name
    if($asset.Count -ne 1 -or (Get-FileHash $path -Algorithm SHA256).Hash -ine $asset[0].sha256){throw "Unverified preview asset: $name"}
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $process=Start-Process -FilePath $path -ArgumentList @('/S',"/D=$install") -WindowStyle Hidden -PassThru -Wait
    if($process.ExitCode -notin 0,3010){throw "Preview setup failed: $variant / $($process.ExitCode)"}
    $measurements+=@{variant=$variant;elapsedSeconds=$watch.Elapsed.TotalSeconds}
    if(!(Test-Path (Join-Path $install 'Voltura.HardTop.exe')) -or !(Test-Path (Join-Path $install 'portable.marker'))){throw 'Preview payload or isolated settings marker is missing.'}
    $registered=Get-ItemProperty $previewKey
    if([IO.Path]::GetFullPath($registered.InstallLocation) -ine [IO.Path]::GetFullPath($install) -or $registered.PackageVariant -ne $variant){throw 'Preview installation registered the wrong location or variant.'}
    if((Test-Path (Join-Path $install 'coreclr.dll')) -ne ($variant -eq 'self-contained')){throw 'Runtime files were not switched with the installer variant.'}
    if((Fingerprint $productionLink) -cne $beforeLink -or (Fingerprint $settings) -cne $beforeSettings -or (StartupEntry) -cne $beforeStartup){throw 'Preview installation changed production integration or preferences.'}
    if(!(Test-Path (Join-Path $programs 'Voltura HardTop Preview.lnk'))){throw 'Preview shortcut is not isolated.'}
    Set-Content -LiteralPath (Join-Path $install 'unrelated-fixture.txt') -Value 'must survive upgrade and uninstall'
}
$uninstall=Start-Process -FilePath (Join-Path $install 'Uninstall.exe') -ArgumentList '/S' -WindowStyle Hidden -PassThru -Wait
if($uninstall.ExitCode -ne 0){throw 'Preview uninstall failed.'}
$deadline=[DateTime]::UtcNow.AddSeconds(15)
while((Test-Path $previewKey) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
if((Test-Path $previewKey) -or (Test-Path (Join-Path $install 'Voltura.HardTop.exe'))){throw 'Preview uninstall did not remove its application.'}
if(!(Test-Path (Join-Path $install 'unrelated-fixture.txt'))){throw 'Preview uninstall removed an unrelated file.'}
if((Fingerprint $productionLink) -cne $beforeLink -or (Fingerprint $settings) -cne $beforeSettings -or (StartupEntry) -cne $beforeStartup){throw 'Preview uninstall changed production integration or preferences.'}
if(Test-Path (Join-Path $programs 'Voltura HardTop Preview.lnk')){throw 'Preview uninstall left its shortcut behind.'}
$measurements|ConvertTo-Json|Write-Output
Write-Host 'PASS preview install, full/lite/full upgrades and uninstall preserve production state and unrelated files.'
# This random fixture was created by this script. Never recursively remove caller paths.
$resolvedInstall=[IO.Path]::GetFullPath($install)
$checksRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/checks'))+[IO.Path]::DirectorySeparatorChar
if(!$resolvedInstall.StartsWith($checksRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected fixture cleanup path.'}
# The uninstaller's temporary helper can finish shortly after removing its registry entry.
Start-Sleep -Milliseconds 300
if(Test-Path -LiteralPath $resolvedInstall){Remove-Item -LiteralPath $resolvedInstall -Recurse -Force}
