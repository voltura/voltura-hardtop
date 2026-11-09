#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$private = Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/Signing'
New-Item -ItemType Directory -Force $private | Out-Null
$key = Join-Path $private 'updates.private.pem'
$config=Join-Path $private 'signing.json'
$signing=if(Test-Path -LiteralPath $config){Get-Content -LiteralPath $config -Raw|ConvertFrom-Json}
. (Join-Path $PSScriptRoot 'update-signing.ps1')
$public = Join-Path $root 'src/Voltura.HardTop/Updates/public.pem'
if (!$signing.updateKeyPath -and !(Test-Path $key)) {
    if (Test-Path $public) { throw 'The public update key exists but the private key is missing. Restore the private key; do not silently rotate a published key.' }
    $rsa = [Security.Cryptography.RSA]::Create(3072)
    try { [IO.File]::WriteAllText($key,$rsa.ExportRSAPrivateKeyPem()); [IO.File]::WriteAllText($public,$rsa.ExportSubjectPublicKeyInfoPem()) } finally { $rsa.Dispose() }
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $key /inheritance:r /grant:r "${identity}:(F)" | Out-Null
    if ($LASTEXITCODE) { throw 'Could not restrict private-key permissions.' }
} else {
    $rsa=[Security.Cryptography.RSA]::Create()
    $verificationKey=[Security.Cryptography.RSA]::Create()
    try {
        Import-HardTopUpdateKey $rsa $private $signing
        $verificationKey.ImportFromPem([IO.File]::ReadAllText($public))
        if ([Convert]::ToBase64String($verificationKey.ExportSubjectPublicKeyInfo()) -cne [Convert]::ToBase64String($rsa.ExportSubjectPublicKeyInfo())) { throw 'Signing keys do not match.' }
    } finally {
        $verificationKey.Dispose()
        $rsa.Dispose()
    }
}
if (!(Test-Path $config)) {
    $certificate=Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.HasPrivateKey -and $_.Subject -like '*Voltura AB*' -and $_.NotAfter -gt (Get-Date) } | Sort-Object NotAfter -Descending | Select-Object -First 1
    if (!$certificate) { $certificate=New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=Voltura AB, O=Voltura AB, C=SE' -FriendlyName 'Voltura HardTop local code signing' -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(3) }
    @{certificateThumbprint=$certificate.Thumbprint;timestampUrl='http://timestamp.digicert.com';allowSelfSigned=($certificate.Subject -eq $certificate.Issuer)} | ConvertTo-Json | Set-Content $config -Encoding utf8
}
& (Join-Path $PSScriptRoot 'branding.ps1')
dotnet restore (Join-Path $root 'src/Voltura.HardTop/Voltura.HardTop.csproj')
if ($LASTEXITCODE) { throw 'Restore failed.' }
Write-Host 'Setup complete. Private signing configuration is outside Git.'
