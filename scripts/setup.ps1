#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$private = Join-Path $env:LOCALAPPDATA 'Voltura/HardTop/Signing'
New-Item -ItemType Directory -Force $private | Out-Null
$key = Join-Path $private 'updates.private.pem'
$public = Join-Path $root 'src/Voltura.HardTop/Updates/public.pem'
if (!(Test-Path $key)) {
    if (Test-Path $public) { throw 'The public update key exists but the private key is missing. Restore the private key; do not silently rotate a published key.' }
    $rsa = [Security.Cryptography.RSA]::Create(3072)
    try { [IO.File]::WriteAllText($key,$rsa.ExportRSAPrivateKeyPem()); [IO.File]::WriteAllText($public,$rsa.ExportSubjectPublicKeyInfoPem()) } finally { $rsa.Dispose() }
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $key /inheritance:r /grant:r "${identity}:(F)" | Out-Null
    if ($LASTEXITCODE) { throw 'Could not restrict private-key permissions.' }
} else {
    $rsa=[Security.Cryptography.RSA]::Create()
    try { $rsa.ImportFromPem([IO.File]::ReadAllText($key)); if (([IO.File]::ReadAllText($public)).Trim() -ne $rsa.ExportSubjectPublicKeyInfoPem().Trim()) { throw 'Signing keys do not match.' } } finally { $rsa.Dispose() }
}
$config=Join-Path $private 'signing.json'
if (!(Test-Path $config)) {
    $certificate=Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.HasPrivateKey -and $_.Subject -like '*Voltura AB*' -and $_.NotAfter -gt (Get-Date) } | Sort-Object NotAfter -Descending | Select-Object -First 1
    if (!$certificate) { $certificate=New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=Voltura AB, O=Voltura AB, C=SE' -FriendlyName 'Voltura HardTop local code signing' -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(3) }
    @{certificateThumbprint=$certificate.Thumbprint;timestampUrl='http://timestamp.digicert.com';allowSelfSigned=($certificate.Subject -eq $certificate.Issuer)} | ConvertTo-Json | Set-Content $config -Encoding utf8
}
& (Join-Path $PSScriptRoot 'branding.ps1')
dotnet restore (Join-Path $root 'src/Voltura.HardTop/Voltura.HardTop.csproj')
if ($LASTEXITCODE) { throw 'Restore failed.' }
Write-Host 'Setup complete. Private signing configuration is outside Git.'
