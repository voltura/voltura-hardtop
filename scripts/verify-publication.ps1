#Requires -Version 7.0
param([Parameter(Mandatory)][string]$ReleaseDirectory,[ValidateRange(1,1800)][int]$TimeoutSeconds=600,[string]$SourceForgeFolder)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'release-files.ps1')
. (Join-Path $PSScriptRoot 'sourceforge-download.ps1')
$verified=Get-VerifiedRelease -ReleaseDirectory $ReleaseDirectory -PublicKeyPath (Join-Path $root 'src/Voltura.HardTop/Updates/public.pem')
$version=$verified.Report.version
$gh=(Get-Command gh.exe -CommandType Application -ErrorAction SilentlyContinue).Source
if(!$gh){$gh=Join-Path $env:LOCALAPPDATA 'Voltura/BuildTools/GitHubCLI/bin/gh.exe'}
$release=& $gh api "repos/voltura/voltura-hardtop/releases/tags/v$version"|ConvertFrom-Json
if($LASTEXITCODE -or $release.draft -or $release.prerelease){throw 'The expected stable GitHub release is not published.'}
foreach($path in $verified.Paths){
    $name=Split-Path $path -Leaf
    $remote=@($release.assets|Where-Object name -CEQ $name)
    $hash=(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if($remote.Count -ne 1 -or $remote[0].size -ne (Get-Item $path).Length -or $remote[0].digest -cne "sha256:$hash"){throw "Published GitHub asset does not match the prepared bytes: $name"}
}
Write-Host "PASS all seven GitHub assets match the prepared release $version."
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$siteReady=$false
$mirrorReady=!$SourceForgeFolder
$mirrored=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$lastFailure=''
while([DateTime]::UtcNow -lt $deadline){
    if(!$siteReady){
        try {
            $page=Invoke-WebRequest 'https://hardtop.voltura.se/' -TimeoutSec 30 -Headers @{'Cache-Control'='no-cache'}
            $siteReady=$page.Content.Contains("/v$version/VolturaHardTop-Setup-$version-win-x64-lite.exe") -and $page.Content.Contains("/v$version/VolturaHardTop-$version-win-x64.zip")
        } catch {$lastFailure=$_.Exception.GetType().Name}
    }
    if(!$mirrorReady){
        if($SourceForgeFolder -notmatch '^v?[0-9]+\.[0-9]+\.[0-9]+$'){throw 'Unexpected SourceForge release folder.'}
        try {
            foreach($local in $verified.Paths){
                $name=Split-Path $local -Leaf
                if($mirrored.Contains($name)){continue}
                Save-SourceForgeAsset -Folder $SourceForgeFolder -Name $name -Destination (Join-Path $root "artifacts/sourceforge/verified/$SourceForgeFolder/$name") -Size (Get-Item $local).Length -Sha256 (Get-FileHash $local -Algorithm SHA256).Hash|Out-Null
                $mirrored.Add($name)|Out-Null
                Write-Host "Verified SourceForge asset: $name"
            }
            $mirrorReady=$mirrored.Count -eq $verified.Paths.Count
        } catch {$lastFailure=$_.Exception.GetType().Name}
    }
    if($siteReady -and $mirrorReady){break}
    Start-Sleep -Seconds 5
}
if(!$siteReady){throw "Product site did not expose both release-specific download links within the verification window. $lastFailure"}
Write-Host "PASS product site points to release $version."
if(!$mirrorReady){throw "SourceForge mirror did not expose the matching release manifest within the verification window. $lastFailure"}
if($SourceForgeFolder){Write-Host 'PASS all seven SourceForge assets match the prepared release.'}
