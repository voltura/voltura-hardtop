#Requires -Version 7.0
param([switch]$SkipBuild,[switch]$Development,[switch]$Candidate,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
if(!$OutputDirectory){$OutputDirectory=Join-Path $root "artifacts/releases/$version"}
if(Test-Path $OutputDirectory){throw 'Output directory exists. Keep published bytes immutable; select a new output directory for another preparation.'}
$payload=Join-Path $root 'artifacts/publish'
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'build.ps1')}
if(!(Test-Path (Join-Path $payload 'Voltura.HardTop.exe'))){throw 'Build output missing.'}
$commit=(& git -C $root rev-parse --verify HEAD 2>$null | Out-String).Trim()
if($LASTEXITCODE -and !($Development -or $Candidate)){throw 'Commit the release source first.'}
if(!($Development -or $Candidate)){
    if((& git -C $root status --porcelain | Out-String).Trim()){throw 'Release packaging requires clean source.'}
    $provenance=Get-Content (Join-Path $payload 'build-provenance.json') -Raw | ConvertFrom-Json
    if(!$provenance.sourceClean -or $provenance.sourceCommit -ne $commit -or $provenance.version -ne $version){throw 'Build output is stale or came from uncommitted source. Rebuild the clean release commit.'}
}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$stage=Join-Path $OutputDirectory 'payload'
Copy-Item $payload $stage -Recurse
$config=Get-Content (Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/Signing/signing.json') -Raw | ConvertFrom-Json
$sign=@{CertificateThumbprint=$config.certificateThumbprint;TimestampUrl=[uri]$config.timestampUrl;AllowSelfSigned=[bool]$config.allowSelfSigned}
& (Join-Path $PSScriptRoot 'sign.ps1') -Path (Join-Path $stage 'Voltura.HardTop.exe') @sign
& (Join-Path $PSScriptRoot 'sign.ps1') -Path (Join-Path $stage 'Voltura.HardTop.dll') @sign
$signatureInfo=& (Join-Path $PSScriptRoot 'verify-signature.ps1') -Path (Join-Path $stage 'Voltura.HardTop.exe') -CertificateThumbprint $config.certificateThumbprint -AllowSelfSigned:$config.allowSelfSigned
$remove=Join-Path $OutputDirectory 'remove-files.nsh'
$deletions=@(Get-ChildItem $stage -File -Recurse | ForEach-Object {'Delete "$INSTDIR\'+[IO.Path]::GetRelativePath($stage,$_.FullName)+'"'})
$deletions+=@(Get-ChildItem $stage -Directory -Recurse | Sort-Object {$_.FullName.Length} -Descending | ForEach-Object {'RMDir "$INSTDIR\'+[IO.Path]::GetRelativePath($stage,$_.FullName)+'"'})
$deletions | Set-Content $remove -Encoding utf8
$installer=Join-Path $OutputDirectory "VolturaHardTop-Setup-$version-win-x64.exe"
$compiler=(Get-Command makensis.exe -ErrorAction SilentlyContinue).Source
if(!$compiler){$compiler=Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe'}
$size=[int][Math]::Ceiling((Get-ChildItem $stage -File -Recurse | Measure-Object Length -Sum).Sum / 1024)
$arguments=@('/V2',"/DVERSION=$version","/DPAYLOAD=$stage","/DOUTPUT=$installer","/DREMOVE_FILES=$remove","/DINSTALL_SIZE_KB=$size",('/DINSTALL_DATE='+(Get-Date -Format yyyyMMdd)))
$arguments+=@("/DPOWERSHELL=$((Get-Command pwsh).Source)","/DSIGN_SCRIPT=$(Join-Path $PSScriptRoot 'sign.ps1')","/DCERT_THUMBPRINT=$($config.certificateThumbprint)","/DTIMESTAMP_URL=$($config.timestampUrl)")
if($config.allowSelfSigned){$arguments+='/DALLOW_SELF_SIGNED'}
if($Development){$arguments+='/DDEVELOPMENT'}
& $compiler @arguments (Join-Path $root 'installer/HardTop.nsi')
if($LASTEXITCODE){throw 'NSIS compilation failed.'}
& (Join-Path $PSScriptRoot 'sign.ps1') -Path $installer @sign
New-Item -ItemType File -Path (Join-Path $stage 'portable.marker') | Out-Null
$zip=Join-Path $OutputDirectory "VolturaHardTop-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$asset=Get-Item $installer
$manifest=@{schema=1;product='VolturaHardTop';architecture='win-x64';version=$version;assets=@(@{name=$asset.Name;size=$asset.Length;sha256=(Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()})}
$manifestPath=Join-Path $OutputDirectory "VolturaHardTop-Update-$version.json"
[IO.File]::WriteAllText($manifestPath,($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$rsa=[Security.Cryptography.RSA]::Create()
try{$rsa.ImportFromPem([IO.File]::ReadAllText((Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/Signing/updates.private.pem')));$signature=$rsa.SignData([IO.File]::ReadAllBytes($manifestPath),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss);[IO.File]::WriteAllBytes((Join-Path $OutputDirectory "VolturaHardTop-Update-$version.sig"),$signature)}finally{$rsa.Dispose()}
if(!($Development -or $Candidate)){
    $source=Join-Path $OutputDirectory "VolturaHardTop-Source-$version.zip"
    & git -C $root archive --format=zip "--prefix=VolturaHardTop-$version/" "--output=$source" $commit
    if($LASTEXITCODE){throw 'Tagged source archive preparation failed.'}
}
$assets=@(Get-ChildItem $OutputDirectory -File | Where-Object {$_.Extension -in '.exe','.zip','.json','.sig' -and $_.Name -ne 'release-manifest.json'} | ForEach-Object {@{name=$_.Name;size=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
@{version=$version;sourceCommit=$commit;development=[bool]($Development -or $Candidate);signing=$signatureInfo.trust;signer=$config.certificateThumbprint;assets=$assets} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutputDirectory 'release-manifest.json') -Encoding utf8
Write-Host "Signed installer, portable ZIP and signed update metadata prepared: $OutputDirectory"
