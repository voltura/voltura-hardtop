# Microsoft runtime bootstrap only. Never executes the downloaded file.
param([Parameter(Mandatory)][string]$Destination, [switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if (!$VerifyOnly) {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -TimeoutSec 300 -Uri 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe' -OutFile $Destination
        if ((Get-Item -LiteralPath $Destination).Length -le 0) { exit 12 }
        exit 0
    } catch { exit 11 }
}
try {
    $signature = Get-AuthenticodeSignature -LiteralPath $Destination
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) { exit 21 }
    if ($null -eq $signature.SignerCertificate) { exit 22 }
    if ($signature.SignerCertificate.SubjectName.Name -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)') { exit 23 }
    exit 0
} catch { exit 21 }
