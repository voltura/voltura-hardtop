# Native VERSIONINFO follows Worlds; apply before signing the .NET apphost.
param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
$parts=$Version.Split('.')
if($parts.Count -ne 3 -or @($parts|Where-Object {$_ -notmatch '^\d+$' -or [int]$_ -gt 65535}).Count){throw 'Version must have three numeric parts between 0 and 65535.'}
$resourceCompiler=Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') -Filter rc.exe -Recurse -File | Where-Object {$_.Directory.Name -eq 'x64'} | Sort-Object FullName -Descending | Select-Object -First 1
if(!$resourceCompiler){throw 'Windows SDK resource compiler rc.exe is required.'}
$scratch=Join-Path (Split-Path $Path -Parent) 'metadata'
New-Item -ItemType Directory -Force $scratch|Out-Null
$rc=Join-Path $scratch 'version.rc';$res=Join-Path $scratch 'version.res';$quad=($parts+@('0')) -join ','
@"
#pragma code_page(65001)
1 VERSIONINFO
 FILEVERSION $quad
 PRODUCTVERSION $quad
 FILEOS 0x40004
 FILETYPE 1
BEGIN
 BLOCK "StringFileInfo"
 BEGIN
  BLOCK "040904B0"
  BEGIN
   VALUE "CompanyName", "Voltura AB"
   VALUE "FileDescription", "Voltura HardTop"
   VALUE "FileVersion", "$Version.0"
   VALUE "InternalName", "Voltura.HardTop"
   VALUE "OriginalFilename", "Voltura.HardTop.exe"
   VALUE "ProductName", "Voltura HardTop"
   VALUE "ProductVersion", "$Version"
   VALUE "LegalCopyright", "Copyright © 2021-2026 Voltura AB"
   VALUE "Comments", "Developer: Joakim Skoglund; Website: https://hardtop.voltura.se/; Support: https://github.com/voltura/voltura-hardtop/issues; License: MIT; Address: Voltura AB, Hästholmsvägen 33, SE-131 71 Nacka, Sweden"
  END
 END
 BLOCK "VarFileInfo"
 BEGIN
  VALUE "Translation", 0x409, 1200
 END
END
"@ | Set-Content $rc -Encoding utf8
& $resourceCompiler.FullName /nologo /fo $res $rc
if($LASTEXITCODE){throw 'Version resource compilation failed.'}
if(!('HardTopVersionResource' -as [type])){Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HardTopVersionResource {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr BeginUpdateResource(string file,bool delete);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool UpdateResource(IntPtr update,IntPtr type,IntPtr name,ushort language,byte[] data,uint size);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool EndUpdateResource(IntPtr update,bool discard);
 public static void Apply(string path,byte[] data) {
  var handle=BeginUpdateResource(path,false); if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
  try {
   // The .NET apphost initially has a language-neutral VERSIONINFO.
   if(!UpdateResource(handle,(IntPtr)16,(IntPtr)1,0,null,0)) { var error=Marshal.GetLastWin32Error(); if(error!=1813 && error!=1815)throw new System.ComponentModel.Win32Exception(error); }
   if(!UpdateResource(handle,(IntPtr)16,(IntPtr)1,1033,data,(uint)data.Length))throw new System.ComponentModel.Win32Exception();
  } catch { EndUpdateResource(handle,true); throw; }
  if(!EndUpdateResource(handle,false))throw new System.ComponentModel.Win32Exception();
 }
}
'@}
$bytes=[IO.File]::ReadAllBytes($res);$offset=0;$data=$null
while($offset+8 -le $bytes.Length){$length=[BitConverter]::ToInt32($bytes,$offset);$header=[BitConverter]::ToInt32($bytes,$offset+4);if($header -lt 8 -or $offset+$header+$length -gt $bytes.Length){throw 'Invalid compiled resource.'};if($length -gt 0){$data=[byte[]]$bytes[($offset+$header)..($offset+$header+$length-1)];break};$offset=($offset+$header+$length+3) -band -4}
if(!$data){throw 'Compiled version resource missing.'}
[HardTopVersionResource]::Apply([IO.Path]::GetFullPath($Path),$data)
$metadata=[Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($Path))
if($metadata.OriginalFilename -ne 'Voltura.HardTop.exe' -or $metadata.ProductVersion -ne $Version -or $metadata.FileDescription -ne 'Voltura HardTop'){throw 'Apphost metadata verification failed.'}
# Only this script's two scratch files are removed; leave unrelated output untouched.
Remove-Item -LiteralPath $rc,$res
Remove-Item -LiteralPath $scratch
Write-Host 'Verified native apphost metadata (Worlds conventions).'
