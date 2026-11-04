#Requires -Version 7.0
param([switch]$Publish)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$repository='voltura/voltura-hardtop'
$site=Join-Path $root 'site'
foreach($name in @('index.html','style.css','CNAME','.nojekyll')){if(!(Test-Path (Join-Path $site $name))){throw "Site file missing: $name"}}
if(!$Publish){Write-Host "Static Pages source is ready: $site. Add -Publish to deploy.";return}
$scratch=Join-Path $root 'artifacts/pages';New-Item -ItemType Directory -Force $scratch|Out-Null
function Api([string]$Method,[string]$Endpoint,$Body){
    $request=Join-Path $scratch ([guid]::NewGuid().ToString('N')+'.json')
    try{
        $ghArguments=@('api','--method',$Method,$Endpoint)
        if($null -ne $Body){[IO.File]::WriteAllText($request,($Body|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false));$ghArguments+=@('--input',$request)}
        $result=& gh @ghArguments
        if($LASTEXITCODE){throw "GitHub API request failed: $Method $Endpoint"}
        if($result){return (($result|Out-String)|ConvertFrom-Json)}
    }finally{if(Test-Path $request){Remove-Item -LiteralPath $request}}
}
$refs=@(Api GET "repos/$repository/git/matching-refs/heads/gh-pages" $null)
$existing=@($refs|Where-Object ref -eq 'refs/heads/gh-pages')
$parent=if($existing.Count){$existing[0].object.sha}else{$null}
$entries=@()
foreach($file in Get-ChildItem $site -File -Recurse -Force){
    $blob=Api POST "repos/$repository/git/blobs" @{encoding='base64';content=[Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))}
    $entries+=@{path=[IO.Path]::GetRelativePath($site,$file.FullName).Replace('\','/');mode='100644';type='blob';sha=$blob.sha}
}
$tree=Api POST "repos/$repository/git/trees" @{tree=$entries}
$body=@{message='Publish Voltura HardTop product website';tree=$tree.sha;parents=@()}
if($parent){$body.parents=@($parent)}
$commit=Api POST "repos/$repository/git/commits" $body
if($parent){Api PATCH "repos/$repository/git/refs/heads/gh-pages" @{sha=$commit.sha;force=$false}|Out-Null}
else{Api POST "repos/$repository/git/refs" @{ref='refs/heads/gh-pages';sha=$commit.sha}|Out-Null}
$repo=Api GET "repos/$repository" $null
$method=if($repo.has_pages){'PUT'}else{'POST'}
Api $method "repos/$repository/pages" @{build_type='legacy';source=@{branch='gh-pages';path='/'}}|Out-Null
Api PUT "repos/$repository/pages" @{cname='hardtop.voltura.se'}|Out-Null
Write-Host 'Published GitHub Pages source. Configure DNS, then enable HTTPS once the certificate is issued.'
