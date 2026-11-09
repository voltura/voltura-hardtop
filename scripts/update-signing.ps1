function Import-HardTopUpdateKey([Security.Cryptography.RSA]$Key, [string]$SigningDirectory, $Config)
{
    $path = if ($Config.updateKeyPath) { $Config.updateKeyPath } else { Join-Path $SigningDirectory 'updates.private.pem' }
    $pem = [IO.File]::ReadAllText($path)
    if ($pem.Contains('-----BEGIN ENCRYPTED PRIVATE KEY-----'))
    {
        $variable = $Config.updateKeyPassphraseVariable
        $passphrase = if ($variable) { [Environment]::GetEnvironmentVariable($variable) }
        if ([string]::IsNullOrWhiteSpace($passphrase)) { throw 'Set the configured update-key passphrase environment variable.' }
        $Key.ImportFromEncryptedPem($pem, $passphrase)
    }
    else
    {
        $Key.ImportFromPem($pem)
    }
}
