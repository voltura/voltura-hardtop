#Requires -Version 7.0
param([switch]$SkipBuild,[switch]$Development,[switch]$Candidate,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
if(!$OutputDirectory){$OutputDirectory=Join-Path $root "artifacts/releases/$version"}
if(Test-Path $OutputDirectory){throw 'Output directory exists. Keep published bytes immutable; select a new output directory for another preparation.'}
$commit=(& git -C $root rev-parse --verify HEAD 2>$null | Out-String).Trim()
if($LASTEXITCODE -and !($Development -or $Candidate)){throw 'Commit the release source first.'}
if(!($Development -or $Candidate) -and (& git -C $root status --porcelain | Out-String).Trim()){throw 'Release packaging requires clean source.'}
# Check the established update identity before building or creating output. A different
# private key would produce installers that existing copies can never authenticate.
$signingDirectory=Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/Signing'
$updateKeyPath=Join-Path $signingDirectory 'updates.private.pem'
$updateKey=[Security.Cryptography.RSA]::Create()
try {
    $updateKey.ImportFromPem([IO.File]::ReadAllText($updateKeyPath))
    $publishedKey=[Security.Cryptography.RSA]::Create()
    try {
        $publishedKey.ImportFromPem([IO.File]::ReadAllText((Join-Path $root 'src/Voltura.HardTop/Updates/public.pem')))
        if([Convert]::ToBase64String($updateKey.ExportSubjectPublicKeyInfo()) -cne [Convert]::ToBase64String($publishedKey.ExportSubjectPublicKeyInfo())){throw 'Update signing key does not match the published verification key. Restore the original private key.'}
    } finally {$publishedKey.Dispose()}
} finally {$updateKey.Dispose()}
$payloadRoot=Join-Path $root "artifacts/builds/$version/$commit"
if(!$SkipBuild){
    & (Join-Path $PSScriptRoot 'build.ps1')
    & (Join-Path $PSScriptRoot 'build.ps1') -FrameworkDependent
}
foreach($variant in 'self-contained','lite'){
    $payload=Join-Path $payloadRoot $variant
    if(!(Test-Path (Join-Path $payload 'Voltura.HardTop.exe'))){throw "Build output missing: $variant"}
    $provenance=Get-Content (Join-Path $payload 'build-provenance.json') -Raw | ConvertFrom-Json
    if($provenance.version -ne $version -or $provenance.selfContained -ne ($variant -eq 'self-contained')){throw "Incorrect build variant: $variant"}
    if(!($Development -or $Candidate) -and (!$provenance.sourceClean -or $provenance.sourceCommit -ne $commit)){throw 'Build output is stale or came from uncommitted source. Rebuild the clean release commit.'}
}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$config=Get-Content (Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/Signing/signing.json') -Raw | ConvertFrom-Json
$sign=@{CertificateThumbprint=$config.certificateThumbprint;TimestampUrl=[uri]$config.timestampUrl;AllowSelfSigned=[bool]$config.allowSelfSigned}
$installerAssets=@()
$signedFiles=@{}
$signingOperations=0
$signatureReuses=0
$fullPayload=Join-Path $payloadRoot 'self-contained'
$allFiles=@(Get-ChildItem $fullPayload -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($fullPayload,$_.FullName)})
if($Development){$allFiles+=@('portable.marker')}
foreach($variant in 'self-contained','lite'){
    $payload=Join-Path $payloadRoot $variant
    $stage=Join-Path $OutputDirectory "payload-$variant"
    Copy-Item $payload $stage -Recurse
    # Preview installers keep settings beside their isolated payload and never
    # become eligible for production automatic updates.
    if($Development){New-Item -ItemType File -Path (Join-Path $stage 'portable.marker') -Force|Out-Null}
    foreach($name in 'Voltura.HardTop.exe','Voltura.HardTop.dll'){
        $binary=Join-Path $stage $name
        $unsignedHash=(Get-FileHash $binary -Algorithm SHA256).Hash
        if($signedFiles.ContainsKey($unsignedHash)){
            Copy-Item -LiteralPath $signedFiles[$unsignedHash] -Destination $binary
            $signatureReuses++
        }else{
            & (Join-Path $PSScriptRoot 'sign.ps1') -Path $binary @sign
            $signedFiles[$unsignedHash]=$binary
            $signingOperations++
        }
    }
    $signatureInfo=& (Join-Path $PSScriptRoot 'verify-signature.ps1') -Path (Join-Path $stage 'Voltura.HardTop.exe') -CertificateThumbprint $config.certificateThumbprint -AllowSelfSigned:$config.allowSelfSigned
    # Own only explicitly known payload files, including runtime files left by the full installer.
    $remove=Join-Path $OutputDirectory "remove-$variant.nsh"
    $allFiles | ForEach-Object {'Delete "$INSTDIR\'+$_+'"'} | Set-Content $remove -Encoding utf8
    Get-ChildItem $fullPayload -Directory -Recurse | Sort-Object {$_.FullName.Length} -Descending | ForEach-Object {'RMDir "$INSTDIR\'+[IO.Path]::GetRelativePath($fullPayload,$_.FullName)+'"'} | Add-Content $remove -Encoding utf8
    $obsolete=Join-Path $OutputDirectory "obsolete-$variant.nsh"
    $currentFiles=@(Get-ChildItem $stage -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($stage,$_.FullName)})
    @('; Remove only owned runtime files when switching from full to lite.') + @($allFiles | Where-Object {$_ -notin $currentFiles} | ForEach-Object {'Delete "$INSTDIR\'+$_+'"'}) | Set-Content $obsolete -Encoding utf8
    $suffix=if($variant -eq 'lite'){'-lite'}else{''}
    $installer=Join-Path $OutputDirectory "VolturaHardTop-Setup-$version-win-x64$suffix.exe"
    $compiler=(Get-Command makensis.exe -ErrorAction SilentlyContinue).Source
    if(!$compiler){$compiler=Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe'}
    $size=[int][Math]::Ceiling((Get-ChildItem $stage -File -Recurse | Measure-Object Length -Sum).Sum / 1024)
    $arguments=@('/V2',"/DVERSION=$version","/DPAYLOAD=$stage","/DOUTPUT=$installer","/DREMOVE_FILES=$remove","/DOBSOLETE_FILES=$obsolete","/DINSTALL_SIZE_KB=$size",('/DINSTALL_DATE='+(Get-Date -Format yyyyMMdd)))
    $arguments+=@("/DPOWERSHELL=$((Get-Command pwsh).Source)","/DSIGN_SCRIPT=$(Join-Path $PSScriptRoot 'sign.ps1')","/DCERT_THUMBPRINT=$($config.certificateThumbprint)","/DTIMESTAMP_URL=$($config.timestampUrl)")
    if($variant -eq 'lite'){
        $runtime=Get-Content (Join-Path $stage 'Voltura.HardTop.runtimeconfig.json') -Raw | ConvertFrom-Json
        $minimum=@($runtime.runtimeOptions.frameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App')[0].version
        if(!$minimum){throw 'Desktop runtime requirement missing from framework-dependent payload.'}
        $arguments+=@('/DFRAMEWORK_DEPENDENT',"/DMINIMUM_RUNTIME=$minimum")
    }
    if($config.allowSelfSigned){$arguments+='/DALLOW_SELF_SIGNED'}
    if($Development){$arguments+='/DDEVELOPMENT'}
    & $compiler @arguments (Join-Path $root 'installer/HardTop.nsi')
    if($LASTEXITCODE){throw 'NSIS compilation failed.'}
    & (Join-Path $PSScriptRoot 'sign.ps1') -Path $installer @sign
    $asset=Get-Item $installer
    $installerAssets+=@{name=$asset.Name;size=$asset.Length;sha256=(Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()}
    if($variant -eq 'self-contained'){
        New-Item -ItemType File -Path (Join-Path $stage 'portable.marker') -Force | Out-Null
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $OutputDirectory "VolturaHardTop-$version-win-x64.zip") -CompressionLevel Optimal
    }
}
$manifest=@{schema=1;product='VolturaHardTop';architecture='win-x64';version=$version;assets=$installerAssets}
$manifestPath=Join-Path $OutputDirectory "VolturaHardTop-Update-$version.json"
[IO.File]::WriteAllText($manifestPath,($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$rsa=[Security.Cryptography.RSA]::Create()
try{$rsa.ImportFromPem([IO.File]::ReadAllText($updateKeyPath));$signature=$rsa.SignData([IO.File]::ReadAllBytes($manifestPath),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss);[IO.File]::WriteAllBytes((Join-Path $OutputDirectory "VolturaHardTop-Update-$version.sig"),$signature)}finally{$rsa.Dispose()}
if(!($Development -or $Candidate)){
    $source=Join-Path $OutputDirectory "VolturaHardTop-Source-$version.zip"
    & git -C $root archive --format=zip "--prefix=VolturaHardTop-$version/" "--output=$source" $commit
    if($LASTEXITCODE){throw 'Tagged source archive preparation failed.'}
}
$assets=@(Get-ChildItem $OutputDirectory -File | Where-Object {$_.Extension -in '.exe','.zip','.json','.sig' -and $_.Name -ne 'release-manifest.json'} | ForEach-Object {@{name=$_.Name;size=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
@{version=$version;sourceCommit=$commit;development=[bool]($Development -or $Candidate);packageMode=$(if($Development){'preview'}elseif($Candidate){'candidate'}else{'release'});signing=$signatureInfo.trust;signer=$config.certificateThumbprint;preparation=@{payloadSigningOperations=$signingOperations;reusedPayloadSignatures=$signatureReuses};assets=$assets} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutputDirectory 'release-manifest.json') -Encoding utf8
Write-Host "Signed installers, portable ZIP and signed update metadata prepared: $OutputDirectory"
