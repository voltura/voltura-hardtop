#Requires -Version 7.0
param([switch]$Publish,[switch]$CheckRemote)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$repository='voltura/voltura-hardtop'
$site=Join-Path $root 'site'
. (Join-Path $PSScriptRoot 'git-blob.ps1')
$githubCli=(Get-Command gh.exe -CommandType Application -ErrorAction SilentlyContinue).Source
if(!$githubCli){$githubCli=Join-Path $env:LOCALAPPDATA 'Voltura/BuildTools/GitHubCLI/bin/gh.exe'}
if(!(Test-Path $githubCli)){throw 'Install the official GitHub CLI (gh.exe).'}
foreach($name in @('index.html','style.css','CNAME','.nojekyll')){if(!(Test-Path (Join-Path $site $name))){throw "Site file missing: $name"}}
if(!$Publish -and !$CheckRemote){Write-Host "Static Pages source is ready: $site. Add -CheckRemote to compare without publishing, or -Publish to deploy.";return}
$scratch=Join-Path $root 'artifacts/pages';New-Item -ItemType Directory -Force $scratch|Out-Null
function Api([string]$Method,[string]$Endpoint,$Body,[switch]$AllowMissing){
    $request=Join-Path $scratch ([guid]::NewGuid().ToString('N')+'.json')
    $errors=$request+'.stderr'
    try{
        $ghArguments=@('api','--method',$Method,$Endpoint)
        if($null -ne $Body){[IO.File]::WriteAllText($request,($Body|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$ghArguments+=@('--input',$request)}
        $result=& $githubCli @ghArguments 2>$errors
        if($LASTEXITCODE){
            if($AllowMissing -and (Get-Content $errors -Raw) -match 'HTTP 404'){return $null}
            throw "GitHub API request failed: $Method $Endpoint"
        }
        if($result){return (($result|Out-String)|ConvertFrom-Json)}
    }finally{foreach($temporary in $request,$errors){if(Test-Path $temporary){Remove-Item -LiteralPath $temporary}}}
}
$refs=@(Api GET "repos/$repository/git/matching-refs/heads/gh-pages" $null)
$existing=@($refs|Where-Object ref -eq 'refs/heads/gh-pages')
$parent=if($existing.Count){$existing[0].object.sha}else{$null}
$remoteFiles=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
if($parent){
    $remoteCommit=Api GET "repos/$repository/git/commits/$parent" $null
    $remoteTree=Api GET "repos/$repository/git/trees/$($remoteCommit.tree.sha)?recursive=1" $null
    if($remoteTree.truncated){throw 'Remote site tree is incomplete; refusing to replace it.'}
    foreach($entry in $remoteTree.tree | Where-Object type -eq 'blob'){$remoteFiles[$entry.path]=$entry.sha}
}
$entries=@()
$changed=0
$files=@(Get-ChildItem $site -File -Recurse -Force)
foreach($file in $files){
    $relative=[IO.Path]::GetRelativePath($site,$file.FullName).Replace('\','/')
    $sha=Get-GitBlobHash $file.FullName
    if(!$remoteFiles.ContainsKey($relative) -or $remoteFiles[$relative] -cne $sha){
        $changed++
        if($Publish){$blob=Api POST "repos/$repository/git/blobs" @{encoding='base64';content=[Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))};$sha=$blob.sha}
    }
    $entries+=@{path=$relative;mode='100644';type='blob';sha=$sha}
    $remoteFiles.Remove($relative)|Out-Null
}
Write-Host "Site comparison: $changed changed/new, $($remoteFiles.Count) removed, $($files.Count-$changed) unchanged files."
if(!$Publish){return}
if($changed -or $remoteFiles.Count -or !$parent){
    $tree=Api POST "repos/$repository/git/trees" @{tree=$entries}
    $body=@{message='Publish Voltura HardTop product website';tree=$tree.sha;parents=@()}
    if($parent){$body.parents=@($parent)}
    $commit=Api POST "repos/$repository/git/commits" $body
    if($parent){Api PATCH "repos/$repository/git/refs/heads/gh-pages" @{sha=$commit.sha;force=$false}|Out-Null}
    else{Api POST "repos/$repository/git/refs" @{ref='refs/heads/gh-pages';sha=$commit.sha}|Out-Null}
}else{Write-Host 'Site content is unchanged; no upload or commit needed.'}
$pages=Api GET "repos/$repository/pages" $null -AllowMissing
$configuration=@{build_type='legacy';source=@{branch='gh-pages';path='/'}}
if(!$pages){Api POST "repos/$repository/pages" $configuration|Out-Null}
elseif($pages.build_type -ne 'legacy' -or $pages.source.branch -ne 'gh-pages' -or $pages.source.path -ne '/'){
    Api PUT "repos/$repository/pages" $configuration|Out-Null
}
if(!$pages -or $pages.cname -ne 'hardtop.voltura.se'){Api PUT "repos/$repository/pages" @{cname='hardtop.voltura.se'}|Out-Null}
Write-Host 'Published GitHub Pages source. Existing DNS and HTTPS settings are preserved.'
