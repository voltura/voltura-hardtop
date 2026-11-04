param([Parameter(Mandatory)][string]$InstallDirectory,[switch]$RemoveSettings)
$ErrorActionPreference='Stop'
$expected=[IO.Path]::GetFullPath((Join-Path $InstallDirectory 'Voltura.HardTop.exe'))
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
foreach($process in Get-Process -Name Voltura.HardTop -ErrorAction SilentlyContinue){
    try{$path=$process.Path}catch{continue}
    if($path -and [IO.Path]::GetFullPath($path) -ieq $expected){
        try{$signal=[Threading.EventWaitHandle]::OpenExisting("Local\Voltura.HardTop.v1.$sid.Exit.$($process.Id)");try{$signal.Set()|Out-Null}finally{$signal.Dispose()}}catch{throw 'Please exit Voltura HardTop and try again.'}
        if(!$process.WaitForExit(15000)){throw 'Voltura HardTop did not close. Please exit it and try again.'}
    }
}
if($RemoveSettings){
    $data=Join-Path $env:LOCALAPPDATA 'Voltura/HardTop'
    # Deliberately preserve Signing/ and its private keys. Delete only app-owned data files.
    foreach($file in @('settings.json','settings.json.pending')){Remove-Item -LiteralPath (Join-Path $data $file) -Force -ErrorAction SilentlyContinue}
    Get-ChildItem -LiteralPath $data -Filter 'settings.json.recovered-*' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    $updates=Join-Path $data 'Updates/pending'
    if(Test-Path -LiteralPath $updates){Get-ChildItem -LiteralPath $updates -File | Where-Object {$_.Name -match '^(VolturaHardTop-Setup-[0-9.]+-win-x64\.exe|manifest\.(json|pending)|signature\.sig|installer\.pending)$'} | Remove-Item -Force}
}
