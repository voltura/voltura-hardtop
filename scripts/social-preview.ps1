param([string]$OutputPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputPath){$OutputPath=Join-Path $root 'site/assets/social-v2.png'}
$bitmap=[Drawing.Bitmap]::new(2400,1260,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode='AntiAlias';$g.PixelOffsetMode='HighQuality';$g.InterpolationMode='HighQualityBicubic';$g.TextRenderingHint='AntiAliasGridFit'
$g.ScaleTransform(2,2)
function Color([string]$Hex){[Drawing.ColorTranslator]::FromHtml($Hex)}
function Box([float]$X,[float]$Y,[float]$W,[float]$H,[float]$Radius,[Drawing.Color]$Color){
    $path=[Drawing.Drawing2D.GraphicsPath]::new();$diameter=2*$Radius
    $path.AddArc($X,$Y,$diameter,$diameter,180,90);$path.AddArc(($X+$W-$diameter),$Y,$diameter,$diameter,270,90)
    $path.AddArc(($X+$W-$diameter),($Y+$H-$diameter),$diameter,$diameter,0,90);$path.AddArc($X,($Y+$H-$diameter),$diameter,$diameter,90,90);$path.CloseFigure()
    $brush=[Drawing.SolidBrush]::new($Color);$g.FillPath($brush,$path);$brush.Dispose();$path.Dispose()
}
function Label([string]$Text,[float]$X,[float]$Y,[float]$Size,[string]$Color,[bool]$Bold=$false){
    $font=[Drawing.Font]::new('Segoe UI',$Size,$(if($Bold){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}),[Drawing.GraphicsUnit]::Pixel)
    $brush=[Drawing.SolidBrush]::new((Color $Color));$g.DrawString($Text,$font,$brush,$X,$Y);$font.Dispose();$brush.Dispose()
}
try {
    $g.Clear((Color '#F5F7FC'))
    Box 594 -80 720 800 90 (Color '#E7ECFF')
    Box 1010 -250 290 1050 80 (Color '#DDE5FF')
    $icon=[Drawing.Image]::FromFile((Join-Path $root 'site/assets/icon.png'))
    try {$g.DrawImage($icon,58,48,58,58)} finally {$icon.Dispose()}
    Label 'VOLTURA' 130 63 24 '#3656E8' $true
    Label 'HardTop' 53 143 88 '#18213A' $true
    Label 'Keep a window.' 59 278 40 '#18213A' $true
    Label 'Keep your focus.' 59 329 40 '#3656E8' $true
    Label 'Pin the windows you need above the rest.' 62 414 22 '#5D6A83'
    Label 'Free for Windows. Open source.' 62 509 21 '#5D6A83'
    Label 'hardtop.voltura.se' 62 557 22 '#3656E8' $true
    Box 618 108 565 363 14 ([Drawing.Color]::FromArgb(22,24,33,58))
    Box 611 99 565 363 14 (Color '#111827')
    $app=[Drawing.Image]::FromFile((Join-Path $root 'site/assets/app.png'))
    try {
        $dest=[Drawing.RectangleF]::new(611,99,565,355.14)
        $source=[Drawing.RectangleF]::new(0,0,1400,880)
        $g.DrawImage($app,$dest,$source,[Drawing.GraphicsUnit]::Pixel)
    } finally {$app.Dispose()}
    Label 'Search. Pin. Stay focused.' 632 499 25 '#18213A' $true
    Label 'The real app, with sample windows.' 633 539 18 '#5D6A83'
    $bitmap.Save([IO.Path]::GetFullPath($OutputPath),[Drawing.Imaging.ImageFormat]::Png)
} finally {$g.Dispose();$bitmap.Dispose()}
Write-Host $OutputPath
