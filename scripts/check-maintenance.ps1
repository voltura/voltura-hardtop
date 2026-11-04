#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$scratch=Join-Path $root ('artifacts/checks/maintenance-'+[guid]::NewGuid().ToString('N'))
$originalLocalAppData=$env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA=$scratch
    $data=Join-Path $scratch 'Voltura/HardTop'
    $pending=Join-Path $data 'Updates/pending'
    $signing=Join-Path $data 'Signing'
    New-Item -ItemType Directory -Force $pending,$signing | Out-Null
    foreach($name in 'VolturaHardTop-Setup-1.0.2-win-x64.exe','VolturaHardTop-Setup-1.0.2-win-x64-lite.exe','manifest.json','signature.sig','installer.pending','unrelated.txt') {
        Set-Content -LiteralPath (Join-Path $pending $name) -Value 'fixture'
    }
    Set-Content -LiteralPath (Join-Path $data 'settings.json') -Value '{}'
    Set-Content -LiteralPath (Join-Path $signing 'updates.private.pem') -Value 'fixture, not a real key'
    & (Join-Path $root 'installer/maintenance.ps1') -InstallDirectory (Join-Path $scratch 'not-installed') -RemoveSettings
    $remaining=@(Get-ChildItem $pending -File)
    if($remaining.Count -ne 1 -or $remaining[0].Name -cne 'unrelated.txt'){throw 'Uninstall cleanup must remove both variants and preserve unrelated files.'}
    if(Test-Path (Join-Path $data 'settings.json')){throw 'Uninstall cleanup left settings behind.'}
    if(!(Test-Path (Join-Path $signing 'updates.private.pem'))){throw 'Uninstall cleanup removed signing material.'}
    Write-Host 'PASS uninstall cleanup removes both variants and preserves signing and unrelated files.'
    $previewData=Join-Path $scratch 'preview/Data'
    New-Item -ItemType Directory -Force $previewData|Out-Null
    Set-Content -LiteralPath (Join-Path $data 'settings.json') -Value 'production fixture'
    Set-Content -LiteralPath (Join-Path $previewData 'settings.json') -Value 'preview fixture'
    & (Join-Path $root 'installer/maintenance.ps1') -InstallDirectory (Join-Path $scratch 'preview') -DataDirectory $previewData -RemoveSettings
    if((Test-Path (Join-Path $previewData 'settings.json')) -or !(Test-Path (Join-Path $data 'settings.json'))){throw 'Preview cleanup must preserve production settings.'}
    Write-Host 'PASS preview cleanup uses isolated data and preserves production settings.'

    # A mismatched key must fail before package creation, even in preview mode.
    $rsa=[Security.Cryptography.RSA]::Create(2048)
    try {[IO.File]::WriteAllText((Join-Path $signing 'updates.private.pem'),$rsa.ExportRSAPrivateKeyPem())} finally {$rsa.Dispose()}
    $output=Join-Path $scratch 'package'
    $rejected=$false
    try {& (Join-Path $PSScriptRoot 'package.ps1') -Development -SkipBuild -OutputDirectory $output}
    catch {if($_.Exception.Message -notlike 'Update signing key does not match*'){throw};$rejected=$true}
    if(!$rejected -or (Test-Path $output)){throw 'A mismatched update key was not rejected before packaging.'}
    Write-Host 'PASS packaging rejects a mismatched update key before creating release output.'
} finally {
    $env:LOCALAPPDATA=$originalLocalAppData
    $resolvedScratch=[IO.Path]::GetFullPath($scratch)
    $checksRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/checks'))+[IO.Path]::DirectorySeparatorChar
    if(!$resolvedScratch.StartsWith($checksRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected fixture cleanup path.'}
    if(Test-Path -LiteralPath $resolvedScratch){Remove-Item -LiteralPath $resolvedScratch -Recurse -Force}
}
