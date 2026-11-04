# Runs in the x64 Windows PowerShell host; no SDK or application runtime is required.
param([version]$MinimumVersion = '10.0.0')
$ErrorActionPreference = 'Stop'

function Test-DesktopRuntime {
    param([string[]]$Runtimes, [version]$Minimum)
    foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        $found = $false
        foreach ($line in $Runtimes) {
            if ($line -match ('^' + [regex]::Escape($framework) + ' (\d+\.\d+\.\d+) \[')) {
                $installed = [version]$Matches[1]
                if ($installed.Major -eq $Minimum.Major -and $installed.Minor -eq $Minimum.Minor -and $installed -ge $Minimum) { $found = $true }
            }
        }
        if (!$found) { return $false }
    }
    return $true
}

# Dot-sourcing exposes the pure detection function for focused prerequisite checks.
if ($MyInvocation.InvocationName -ne '.') {
    try {
        $dotnet = Join-Path $env:ProgramW6432 'dotnet\dotnet.exe'
        if (!(Test-Path -LiteralPath $dotnet -PathType Leaf)) { exit 1 }
        $runtimes = & $dotnet --list-runtimes
        if ($LASTEXITCODE -ne 0) { exit 1 }
        if (Test-DesktopRuntime -Runtimes $runtimes -Minimum $MinimumVersion) { exit 0 }
        exit 1
    } catch { exit 1 }
}
