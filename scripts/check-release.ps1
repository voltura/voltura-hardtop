#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'release-files.ps1')
. (Join-Path $PSScriptRoot 'git-blob.ps1')
. (Join-Path $PSScriptRoot 'sourceforge-download.ps1')
$scratch=Join-Path $root ('artifacts/checks/release-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $scratch|Out-Null
$rsa=[Security.Cryptography.RSA]::Create(2048)
try {
    $version='1.0.2'
    $key=Join-Path $scratch 'fixture-public.pem'
    [IO.File]::WriteAllText($key,$rsa.ExportSubjectPublicKeyInfoPem())
    $names=@("VolturaHardTop-Setup-$version-win-x64.exe","VolturaHardTop-Setup-$version-win-x64-lite.exe","VolturaHardTop-$version-win-x64.zip","VolturaHardTop-Source-$version.zip")
    foreach($name in $names){[IO.File]::WriteAllText((Join-Path $scratch $name),'fixture, never distributed')}
    $installers=@($names[0..1]|ForEach-Object {$file=Get-Item (Join-Path $scratch $_);@{name=$file.Name;size=$file.Length;sha256=(Get-FileHash $file.FullName -Algorithm SHA256).Hash}})
    $manifest=@{schema=1;product='VolturaHardTop';architecture='win-x64';version=$version;assets=$installers}
    function WriteManifest {
        $bytes=[Text.Encoding]::UTF8.GetBytes(($manifest|ConvertTo-Json -Depth 6))
        [IO.File]::WriteAllBytes((Join-Path $scratch "VolturaHardTop-Update-$version.json"),$bytes)
        [IO.File]::WriteAllBytes((Join-Path $scratch "VolturaHardTop-Update-$version.sig"),$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss))
    }
    function WriteReport {
        $assets=@($names+@("VolturaHardTop-Update-$version.json","VolturaHardTop-Update-$version.sig")|ForEach-Object {$file=Get-Item (Join-Path $scratch $_);@{name=$file.Name;size=$file.Length;sha256=(Get-FileHash $file.FullName -Algorithm SHA256).Hash}})
        $script:fixtureReport=@{version=$version;development=$false;sourceCommit=('a'*40);assets=$assets}
        $script:fixtureReport|ConvertTo-Json -Depth 6|Set-Content (Join-Path $scratch 'release-manifest.json')
    }
    function Reject([string]$Message) {
        $rejected=$false
        try {Get-VerifiedRelease -ReleaseDirectory $scratch -PublicKeyPath $key|Out-Null} catch {$rejected=$true}
        if(!$rejected){throw $Message}
    }
    WriteManifest; WriteReport
    if((Get-VerifiedRelease -ReleaseDirectory $scratch -PublicKeyPath $key).Paths.Count -ne 7){throw 'Complete signed release was not accepted.'}
    $fixtureReport.assets=@($fixtureReport.assets|Select-Object -Skip 1)
    $fixtureReport|ConvertTo-Json -Depth 6|Set-Content (Join-Path $scratch 'release-manifest.json')
    Reject 'Incomplete release asset set was accepted.'
    WriteReport
    $fixtureReport.assets[0].name='../private.pem'
    $fixtureReport|ConvertTo-Json -Depth 6|Set-Content (Join-Path $scratch 'release-manifest.json')
    Reject 'Unexpected asset path was accepted.'
    WriteReport
    $signaturePath=Join-Path $scratch "VolturaHardTop-Update-$version.sig"
    $signature=[IO.File]::ReadAllBytes($signaturePath);$signature[0]=$signature[0] -bxor 1;[IO.File]::WriteAllBytes($signaturePath,$signature)
    WriteReport
    Reject 'An invalid update signature was accepted after the outer hash was updated.'
    WriteManifest; WriteReport
    $manifest.assets[0].sha256='0'*64
    WriteManifest; WriteReport
    Reject 'Signed metadata inconsistent with installer bytes was accepted.'
    Write-Host 'PASS complete release, missing assets, unexpected paths, invalid signatures and inconsistent signed metadata.'
    foreach($path in 'site/index.html','site/assets/app.png','site/.nojekyll'){
        $absolute=Join-Path $root $path
        $expected=(& git -C $root hash-object --no-filters $absolute).Trim()
        if($LASTEXITCODE -or (Get-GitBlobHash $absolute) -cne $expected){throw "Git blob hash mismatch: $path"}
    }
    Write-Host 'PASS streaming site hashes match Git for text, binary and empty files.'
    $download=[uri]'https://downloads.sourceforge.net/project/voltura-hardtop/1.0.2/release-manifest.json'
    $page='<meta http-equiv="refresh" content="5; url=https://downloads.sourceforge.net/project/voltura-hardtop/1.0.2/release-manifest.json?ts=fixture&amp;use_mirror=fixture">'
    $redirect=Get-SourceForgeRefreshUri $page $download
    if(!$redirect -or $redirect.Query -cne '?ts=fixture&use_mirror=fixture'){throw 'SourceForge public download redirect was not decoded.'}
    if(Get-SourceForgeRefreshUri ($page.Replace('downloads.sourceforge.net','example.invalid')) $download){throw 'A foreign download-page redirect was accepted.'}
    if(Get-SourceForgeRefreshUri ($page.Replace('/1.0.2/','/1.0.3/')) $download){throw 'A redirect to a different release asset was accepted.'}
    Write-Host 'PASS SourceForge public download redirects are decoded and restricted to the requested asset.'
} finally {
    $rsa.Dispose()
    $resolved=[IO.Path]::GetFullPath($scratch)
    $checksRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/checks'))+[IO.Path]::DirectorySeparatorChar
    if(!$resolved.StartsWith($checksRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected release fixture cleanup path.'}
    if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
