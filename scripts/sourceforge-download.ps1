function Get-SourceForgeRefreshUri([string]$Html,[uri]$Original) {
    foreach($tag in [regex]::Matches($Html,'(?is)<meta\b[^>]*>')){
        if($tag.Value -notmatch 'http-equiv\s*=\s*["'']refresh["'']'){continue}
        $content=[regex]::Match($tag.Value,'content\s*=\s*["''](?<body>[^"'']+)["'']',[Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if(!$content.Success -or $content.Groups['body'].Value -notmatch '^\s*\d+\s*;\s*url\s*=\s*(.+)$'){continue}
        $address=[Net.WebUtility]::HtmlDecode($Matches[1]).Trim()
        $next=$null
        if([uri]::TryCreate($address,[UriKind]::Absolute,[ref]$next) -and $next.Scheme -ceq 'https' -and !$next.UserInfo -and $next.Host -ceq 'downloads.sourceforge.net' -and $next.AbsolutePath -ceq $Original.AbsolutePath){return $next}
    }
    return $null
}

function Save-SourceForgeAsset {
    param([Parameter(Mandatory)][string]$Folder,[Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][long]$Size,[Parameter(Mandatory)][string]$Sha256)
    if($Folder -notmatch '^v?[0-9]+\.[0-9]+\.[0-9]+$' -or [IO.Path]::GetFileName($Name) -cne $Name -or $Name.Contains('/') -or $Name.Contains('\')){throw 'Unexpected SourceForge download path.'}
    $uri=[uri]"https://downloads.sourceforge.net/project/voltura-hardtop/$Folder/$([uri]::EscapeDataString($Name))"
    $original=$uri
    $destinationPath=[IO.Path]::GetFullPath($Destination)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationPath))|Out-Null
    $pending=$destinationPath+'.'+[guid]::NewGuid().ToString('N')+'.pending'
    try {
        for($attempt=0;$attempt -lt 4;$attempt++){
            $response=Invoke-WebRequest $uri -OutFile $pending -PassThru -TimeoutSec 120 -MaximumRedirection 5
            if(($response.Headers['Content-Type'] -join ',') -match 'text/html'){
                if((Get-Item $pending).Length -gt 2MB){throw 'Unexpected SourceForge download page size.'}
                $next=Get-SourceForgeRefreshUri ([IO.File]::ReadAllText($pending)) $original
                if(!$next -or $next.AbsoluteUri -ceq $uri.AbsoluteUri){throw 'SourceForge returned HTML without a usable public download redirect.'}
                $uri=$next
                continue
            }
            if((Get-Item $pending).Length -ne $Size -or (Get-FileHash $pending -Algorithm SHA256).Hash -ine $Sha256){throw "SourceForge bytes do not match the prepared release: $Name"}
            [IO.File]::Move($pending,$destinationPath,$true)
            return $destinationPath
        }
        throw 'SourceForge download redirect limit exceeded.'
    } finally {if(Test-Path -LiteralPath $pending){Remove-Item -LiteralPath $pending -Force}}
}
