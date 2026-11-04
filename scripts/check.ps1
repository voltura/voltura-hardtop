#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'check-runtime.ps1')
dotnet run --project (Join-Path $root 'tests/HardTop.Checks/HardTop.Checks.csproj') -c Release
if ($LASTEXITCODE) { throw 'Behavior checks failed.' }
dotnet run --project (Join-Path $root 'tests/HardTop.Checks/HardTop.Checks.csproj') -c Release --no-build -- --layout
if ($LASTEXITCODE) { throw 'Layout checks failed.' }
$files=Get-ChildItem (Join-Path $root 'src/Voltura.HardTop/Localization/Translations') -Filter '*.json'
$english=Get-Content (Join-Path $files[0].DirectoryName 'en.json') -Raw | ConvertFrom-Json -AsHashtable
foreach($xaml in Get-ChildItem (Join-Path $root 'src/Voltura.HardTop') -Recurse -Filter '*.xaml' | Where-Object FullName -notmatch '\\(bin|obj)\\') {
    foreach($match in [regex]::Matches([IO.File]::ReadAllText($xaml.FullName),'\{loc:Loc\s+(\w+)\}')) {
        if(!$english.ContainsKey($match.Groups[1].Value)){throw "Unknown UI translation key $($match.Groups[1].Value) in $($xaml.Name)"}
    }
}
foreach($file in $files){$entries=Get-Content $file.FullName -Raw | ConvertFrom-Json -AsHashtable;foreach($key in $english.Keys){if(!$entries.ContainsKey($key)){throw "Missing $key in $($file.Name)"};$expected=@([regex]::Matches($english[$key],'\{\d+\}')|ForEach-Object Value|Sort-Object);$actual=@([regex]::Matches($entries[$key],'\{\d+\}')|ForEach-Object Value|Sort-Object);if(($expected -join ',') -ne ($actual -join ',')){throw "Placeholder mismatch: $key in $($file.Name)"}}}
git -C $root diff --check
if($LASTEXITCODE){throw 'Whitespace check failed.'}
Write-Host 'Behavior, translation and whitespace checks passed.'
