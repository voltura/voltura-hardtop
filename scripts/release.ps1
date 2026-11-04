#Requires -Version 7.0
param([switch]$Publish,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$repository='voltura/voltura-hardtop'
. (Join-Path $PSScriptRoot 'release-files.ps1')
$version=([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
if(!$OutputDirectory){$OutputDirectory=Join-Path $root "artifacts/releases/$version"}
$tag="v$version"
$githubCli=(Get-Command gh.exe -CommandType Application -ErrorAction SilentlyContinue).Source
if(!$githubCli){$githubCli=Join-Path $env:LOCALAPPDATA 'Voltura/BuildTools/GitHubCLI/bin/gh.exe'}
if(!(Test-Path $githubCli)){throw 'Install the official GitHub CLI (gh.exe).'}
function InvokeGit([string[]]$Arguments){$result=& git -C $root @Arguments;if($LASTEXITCODE){throw "Git failed: $($Arguments -join ' ')"};return (($result|Out-String).Trim())}
function InvokeGh([string[]]$Arguments){$result=& $githubCli @Arguments;if($LASTEXITCODE){throw "GitHub failed: $($Arguments -join ' ')"};return $result}
if(InvokeGit -Arguments @('status','--porcelain')){throw 'Commit the reviewed source before release preparation.'}
$head=InvokeGit -Arguments @('rev-parse','HEAD')
$notes=Join-Path $root "docs/release-$version.md"
if(!(Test-Path $notes)){throw 'Reviewed release notes are required before building release assets.'}
if($Publish){
    InvokeGit -Arguments @('fetch','origin','main')|Out-Null
    if((InvokeGit -Arguments @('rev-parse','origin/main')) -ne $head){throw 'Local main must match origin/main.'}
    if((InvokeGit -Arguments @('remote','get-url','origin')) -notmatch 'github\.com[:/]voltura/voltura-hardtop(?:\.git)?$'){throw 'Unexpected publication repository.'}
}
$reportPath=Join-Path $OutputDirectory 'release-manifest.json'
if(!(Test-Path $reportPath)){
    & (Join-Path $PSScriptRoot 'check.ps1')
    & (Join-Path $PSScriptRoot 'build.ps1')
    & (Join-Path $PSScriptRoot 'build.ps1') -FrameworkDependent
    & (Join-Path $PSScriptRoot 'package.ps1') -SkipBuild -OutputDirectory $OutputDirectory
}
$verified=Get-VerifiedRelease -ReleaseDirectory $OutputDirectory -PublicKeyPath (Join-Path $root 'src/Voltura.HardTop/Updates/public.pem')
$report=$verified.Report
if($report.development -or $report.sourceCommit -ne $head -or $report.version -ne $version){throw 'Prepared assets do not match this clean release commit.'}
if(!$Publish){Write-Host "Prepared release $tag. Add -Publish to publish these exact bytes.";return}
$remoteTags=InvokeGit -Arguments @('ls-remote','--tags','origin',"refs/tags/$tag")
if($remoteTags -and $remoteTags.Split()[0] -ne $head){throw 'Remote tag already points to different source.'}
$source=Join-Path $OutputDirectory "VolturaHardTop-Source-$version.zip"
if(!(Test-Path $source) -or !@($report.assets | Where-Object name -eq (Split-Path $source -Leaf)).Count){throw 'Prepared source archive must be included in the release manifest.'}
$releases=InvokeGh -Arguments @('api',"repos/$repository/releases?per_page=100")|ConvertFrom-Json
$existing=@($releases|Where-Object tag_name -eq $tag)
if(!$existing.Count){InvokeGh -Arguments @('release','create',$tag,'--repo',$repository,'--target',$head,'--draft','--title',"Voltura HardTop $version",'--notes-file',$notes)|Out-Null}
$release=@(InvokeGh -Arguments @('api',"repos/$repository/releases?per_page=100")|ConvertFrom-Json|Where-Object tag_name -eq $tag)[0]
if(!$release){throw 'Release draft was not created.'}
if($release.draft -and $release.target_commitish -ne $head){InvokeGh -Arguments @('release','edit',$tag,'--repo',$repository,'--target',$head,'--notes-file',$notes)|Out-Null}
$release=InvokeGh -Arguments @('api',"repos/$repository/releases/$($release.id)")|ConvertFrom-Json
$paths=$verified.Paths
foreach($path in $paths){
    $name=Split-Path $path -Leaf
    $published=@($release.assets|Where-Object name -eq $name)
    if($published.Count){
        $digest='sha256:'+(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if($published[0].digest -ne $digest){throw "Refusing to replace existing release bytes: $name"}
    }else{InvokeGh -Arguments @('release','upload',$tag,$path,'--repo',$repository)|Out-Null}
}
InvokeGh -Arguments @('release','edit',$tag,'--repo',$repository,'--draft=false','--latest')|Out-Null
InvokeGit -Arguments @('fetch','origin','main','--tags')|Out-Null
if((InvokeGit -Arguments @('rev-parse',$tag)) -ne $head -or (InvokeGit -Arguments @('rev-parse','origin/main')) -ne $head){throw 'Published source/tag verification failed.'}
Write-Host "Published $tag from $head. Mirror the same output directory to SourceForge."
