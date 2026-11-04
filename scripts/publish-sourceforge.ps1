#Requires -Version 7.0
param([Parameter(Mandatory)][string]$ReleaseDirectory,[string]$IdentityFile)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'release-files.ps1')
. (Join-Path $PSScriptRoot 'sourceforge-download.ps1')
$verified=Get-VerifiedRelease -ReleaseDirectory $ReleaseDirectory -PublicKeyPath (Join-Path $root 'src/Voltura.HardTop/Updates/public.pem')
$report=$verified.Report
$version=$report.version
$scratch=Join-Path $root 'artifacts/sourceforge';New-Item -ItemType Directory -Force $scratch|Out-Null
$paths=$verified.Paths
$commands=@('-mkdir /home/frs/project/voltura-hardtop/'+$version)
foreach($path in $paths){
    if(!(Test-Path $path)){throw "Asset missing: $path"}
    $name=Split-Path $path -Leaf
    $asset=@($report.assets|Where-Object name -eq $name)
    if($asset.Count -and (Get-FileHash $path -Algorithm SHA256).Hash -ine $asset[0].sha256){throw "Local asset changed: $name"}
    $remote=Join-Path $scratch $name
    try{
        Save-SourceForgeAsset -Folder $version -Name $name -Destination $remote -Size (Get-Item $path).Length -Sha256 (Get-FileHash $path -Algorithm SHA256).Hash|Out-Null
        continue
    }catch [Microsoft.PowerShell.Commands.HttpResponseException]{if([int]$_.Exception.Response.StatusCode -ne 404){throw}}
    $local=[IO.Path]::GetFullPath($path).Replace('\','/')
    $commands+='put "'+$local+'" "/home/frs/project/voltura-hardtop/'+$version+'/.'+$name+'.pending"'
    $commands+='rename "/home/frs/project/voltura-hardtop/'+$version+'/.'+$name+'.pending" "/home/frs/project/voltura-hardtop/'+$version+'/'+$name+'"'
}
$batch=Join-Path $scratch 'upload.sftp'
$commands|Set-Content $batch -Encoding utf8
$arguments=@('-oBatchMode=yes','-b',$batch)
if($IdentityFile){$arguments+=@('-i',$IdentityFile)}
$arguments+='voltura,voltura-hardtop@frs.sourceforge.net'
& sftp @arguments
if($LASTEXITCODE){throw 'SourceForge upload failed. Configure your SourceForge SSH key and verified host key, or upload these exact prepared files in its Files UI.'}
Write-Host 'Mirrored prepared assets. Select the installer as the Windows default in SourceForge Files.'
