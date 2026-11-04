#Requires -Version 7.0
param([switch]$Publish,[string]$ReleaseDirectory,[string]$SourceForgeFolder)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
if(!$ReleaseDirectory){$ReleaseDirectory=Join-Path $root "artifacts/releases/$version"}
if(!$Publish){
    Write-Host "Ready to prepare/publish $version locally, publish changed site files and verify public assets."
    Write-Host 'SourceForge mirrors the GitHub release through its configured release-only webhook.'
    Write-Host 'Add -Publish to deploy; -SourceForgeFolder also waits for a matching mirrored release manifest.'
    return
}
& (Join-Path $PSScriptRoot 'release.ps1') -Publish -OutputDirectory $ReleaseDirectory
& (Join-Path $PSScriptRoot 'publish-pages.ps1') -Publish
$verification=@{ReleaseDirectory=$ReleaseDirectory}
if($SourceForgeFolder){$verification.SourceForgeFolder=$SourceForgeFolder}
& (Join-Path $PSScriptRoot 'verify-publication.ps1') @verification
Write-Host 'GitHub and site publication verified. Check the SourceForge Windows default after a new mirror import.'
