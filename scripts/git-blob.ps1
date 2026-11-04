# Dot-source for Git-compatible content hashing without spawning Git once per asset.
function Get-GitBlobHash([Parameter(Mandatory)][string]$Path) {
    $file=Get-Item -LiteralPath $Path -ErrorAction Stop
    $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA1)
    try {
        $hash.AppendData([Text.Encoding]::UTF8.GetBytes("blob $($file.Length)`0"))
        $inputFile=[IO.File]::OpenRead($file.FullName)
        try {
            $buffer=[byte[]]::new(81920)
            while(($count=$inputFile.Read($buffer,0,$buffer.Length)) -gt 0){$hash.AppendData($buffer,0,$count)}
        } finally {$inputFile.Dispose()}
        return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    } finally {$hash.Dispose()}
}
