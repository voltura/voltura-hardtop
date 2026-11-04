# Shared, read-only validation for every publication destination.
function Get-VerifiedRelease {
    param([Parameter(Mandatory)][string]$ReleaseDirectory,[Parameter(Mandatory)][string]$PublicKeyPath)
    $report=Get-Content (Join-Path $ReleaseDirectory 'release-manifest.json') -Raw|ConvertFrom-Json
    if($report.development){throw 'Development packages cannot be published.'}
    $version=[string]$report.version
    if($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'Invalid release version.'}
    $full="VolturaHardTop-Setup-$version-win-x64.exe"
    $lite="VolturaHardTop-Setup-$version-win-x64-lite.exe"
    $manifestName="VolturaHardTop-Update-$version.json"
    $signatureName="VolturaHardTop-Update-$version.sig"
    $required=@($full,$lite,$manifestName,$signatureName,"VolturaHardTop-$version-win-x64.zip","VolturaHardTop-Source-$version.zip")
    $assets=@($report.assets)
    if($assets.Count -ne $required.Count){throw 'Release must contain both installers, portable/source archives and signed update metadata.'}
    $index=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $paths=@()
    foreach($asset in $assets){
        if($asset.name -cnotin $required -or $index.ContainsKey($asset.name)){throw 'Unexpected or duplicate release asset.'}
        if($asset.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $asset.size -le 0){throw "Invalid asset metadata: $($asset.name)"}
        $path=Join-Path $ReleaseDirectory $asset.name
        if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -ne $asset.size -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $asset.sha256){throw "Prepared asset changed: $($asset.name)"}
        $index.Add($asset.name,$asset)
        $paths+=@($path)
    }
    $manifestBytes=[IO.File]::ReadAllBytes((Join-Path $ReleaseDirectory $manifestName))
    $signature=[IO.File]::ReadAllBytes((Join-Path $ReleaseDirectory $signatureName))
    if($manifestBytes.Length -gt 65536 -or $signature.Length -gt 1024){throw 'Update verification metadata exceeds the application limits.'}
    $rsa=[Security.Cryptography.RSA]::Create()
    try {
        $rsa.ImportFromPem([IO.File]::ReadAllText($PublicKeyPath))
        if(!$rsa.VerifyData($manifestBytes,$signature,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)){throw 'Prepared update signature is invalid.'}
    } finally {$rsa.Dispose()}
    $manifest=[Text.Encoding]::UTF8.GetString($manifestBytes)|ConvertFrom-Json
    if($manifest.schema -ne 1 -or $manifest.product -cne 'VolturaHardTop' -or $manifest.architecture -cne 'win-x64' -or $manifest.version -cne $version -or @($manifest.assets).Count -ne 2){throw 'Prepared update metadata does not identify this release.'}
    foreach($name in $full,$lite){
        $signed=@($manifest.assets|Where-Object name -CEQ $name)
        if($signed.Count -ne 1 -or $signed[0].size -ne $index[$name].size -or $signed[0].sha256 -ine $index[$name].sha256 -or $signed[0].size -gt 250MB){throw "Signed installer metadata does not match the release: $name"}
    }
    return [pscustomobject]@{Report=$report;Paths=$paths+@(Join-Path $ReleaseDirectory 'release-manifest.json')}
}
