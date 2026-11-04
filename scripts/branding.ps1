# Original flat vector branding. Render every frame at its own pixel size.
param([string]$PreviewPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$script:Blue = [Drawing.ColorTranslator]::FromHtml('#3656E8')
$script:Ink = [Drawing.ColorTranslator]::FromHtml('#18213A')
$script:Muted = [Drawing.ColorTranslator]::FromHtml('#63708A')
$script:Paper = [Drawing.ColorTranslator]::FromHtml('#F5F7FC')
function New-RoundedPath([float]$x,[float]$y,[float]$width,[float]$height,[float]$radius) {
    $p=[Drawing.Drawing2D.GraphicsPath]::new();$d=$radius*2
    $p.AddArc($x,$y,$d,$d,180,90);$p.AddArc(($x+$width-$d),$y,$d,$d,270,90)
    $p.AddArc(($x+$width-$d),($y+$height-$d),$d,$d,0,90);$p.AddArc($x,($y+$height-$d),$d,$d,90,90)
    $p.CloseFigure();return $p
}
function New-Canvas([int]$width,[int]$height,[Drawing.Color]$color) {
    $b=[Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($b);$g.SmoothingMode='AntiAlias';$g.PixelOffsetMode='HighQuality'
    $g.CompositingQuality='HighQuality';$g.TextRenderingHint='AntiAliasGridFit';$g.Clear($color)
    return @($b,$g)
}
function Draw-Pin($g,[float]$x,[float]$y,[float]$size,[Drawing.Color]$color) {
    $s=$g.Save();$g.TranslateTransform($x,$y);$g.ScaleTransform(($size/256),($size/256))
    $g.TranslateTransform(116,142);$g.RotateTransform(35)
    $fill=[Drawing.SolidBrush]::new($color);$cap=New-RoundedPath -54 -84 108 30 9
    $g.FillPath($fill,$cap);$g.FillRectangle($fill,-25,-60,50,70)
    $shoulder=[Drawing.PointF[]]@([Drawing.PointF]::new(-25,2),[Drawing.PointF]::new(-56,29),[Drawing.PointF]::new(-56,40),[Drawing.PointF]::new(56,40),[Drawing.PointF]::new(56,29),[Drawing.PointF]::new(25,2))
    $g.FillPolygon($fill,$shoulder)
    $needle=[Drawing.PointF[]]@([Drawing.PointF]::new(-9,35),[Drawing.PointF]::new(9,35),[Drawing.PointF]::new(0,96))
    $g.FillPolygon($fill,$needle);$cap.Dispose();$fill.Dispose();$g.Restore($s)
}
function Draw-Icon($g,[float]$x,[float]$y,[float]$size) {
    $s=$g.Save();$g.TranslateTransform($x,$y);$g.ScaleTransform(($size/256),($size/256))
    $tile=New-RoundedPath 12 12 232 232 52;$fill=[Drawing.SolidBrush]::new($script:Blue)
    $g.FillPath($fill,$tile);Draw-Pin $g 0 0 256 ([Drawing.Color]::White)
    $tile.Dispose();$fill.Dispose();$g.Restore($s)
}
function New-IconFrame([int]$size) {
    $pair=New-Canvas $size $size ([Drawing.Color]::Transparent)
    Draw-Icon $pair[1] 0 0 $size;$pair[1].Dispose();return $pair[0]
}
function Draw-Label($g,[string]$text,[float]$x,[float]$y,[float]$size,[Drawing.Color]$color,[bool]$bold=$false) {
    $weight=if($bold){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}
    $f=[Drawing.Font]::new('Segoe UI',$size,$weight,[Drawing.GraphicsUnit]::Pixel);$brush=[Drawing.SolidBrush]::new($color)
    $g.DrawString($text,$f,$brush,$x,$y);$f.Dispose();$brush.Dispose()
}
$assets=Join-Path $root 'src/Voltura.HardTop/Assets';$site=Join-Path $root 'site/assets';$installer=Join-Path $root 'installer/assets'
New-Item -ItemType Directory -Force $assets,$site,$installer | Out-Null
$brand=New-IconFrame 256
$brand.Save((Join-Path $assets 'Brand.png'),[Drawing.Imaging.ImageFormat]::Png)
$brand.Save((Join-Path $site 'icon.png'),[Drawing.Imaging.ImageFormat]::Png);$brand.Dispose()
$sizes=@(16,20,24,32,40,48,64,128,256);$frames=@()
foreach($size in $sizes){$image=New-IconFrame $size;$stream=[IO.MemoryStream]::new();$image.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$frames+=,$stream.ToArray();$image.Dispose();$stream.Dispose()}
$ico=[IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assets 'App.ico')))
try {
    $ico.Write([uint16]0);$ico.Write([uint16]1);$ico.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$ico.Write([byte]$dimension);$ico.Write([byte]$dimension);$ico.Write([byte]0);$ico.Write([byte]0);$ico.Write([uint16]1);$ico.Write([uint16]32);$ico.Write([uint32]$frames[$i].Length);$ico.Write([uint32]$offset);$offset+=$frames[$i].Length}
    foreach($frame in $frames){$ico.Write([byte[]]$frame)}
} finally {$ico.Dispose()}
Copy-Item (Join-Path $assets 'App.ico') (Join-Path $root 'site/favicon.ico') -Force
# NSIS rail: simple large mark and spacious typography, no decorative pattern.
$pair=New-Canvas 1312 2512 $script:Blue;$b=$pair[0];$g=$pair[1]
Draw-Pin $g 190 250 900 ([Drawing.Color]::White)
Draw-Label $g 'VOLTURA' 132 1375 48 ([Drawing.Color]::FromArgb(210,255,255,255))
Draw-Label $g 'HardTop' 124 1460 116 ([Drawing.Color]::White) $true
Draw-Label $g 'Pin what matters.' 132 1665 60 ([Drawing.Color]::White)
Draw-Label $g 'WINDOWS' 132 2175 37 ([Drawing.Color]::FromArgb(210,255,255,255))
Draw-Label $g 'hardtop.voltura.se' 132 2250 43 ([Drawing.Color]::White)
$b.Save((Join-Path $installer 'wizard.bmp'),[Drawing.Imaging.ImageFormat]::Bmp);$g.Dispose();$b.Dispose()
# Light-first social card.
$pair=New-Canvas 1200 630 $script:Paper;$b=$pair[0];$g=$pair[1]
Draw-Label $g 'VOLTURA' 66 68 26 $script:Blue $true
Draw-Label $g 'HardTop' 59 133 100 $script:Ink $true
Draw-Label $g 'Pin what matters.' 66 282 42 $script:Ink
Draw-Label $g 'Keep the windows you need above the rest.' 66 352 26 $script:Muted
Draw-Label $g 'Free for Windows. Open source.' 66 529 25 $script:Muted
Draw-Icon $g 842 140 292
$b.Save((Join-Path $site 'social.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()
if($PreviewPath){
    New-Item -ItemType Directory -Force (Split-Path $PreviewPath -Parent)|Out-Null
    $pair=New-Canvas 1400 950 $script:Paper;$b=$pair[0];$g=$pair[1]
    Draw-Label $g 'VOLTURA HARDTOP' 52 35 20 $script:Blue $true
    Draw-Label $g 'A clear new identity.' 49 78 43 $script:Ink $true
    Draw-Label $g 'One bold pushpin. Cobalt and white. Built to read beside the Windows clock.' 52 143 22 $script:Muted
    $dark=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#111827'));$light=[Drawing.SolidBrush]::new([Drawing.Color]::White)
    $lp=New-RoundedPath 48 218 1304 300 20;$dp=New-RoundedPath 48 540 1304 300 20
    $g.FillPath($light,$lp);$g.FillPath($dark,$dp)
    Draw-Label $g 'LIGHT TASKBAR' 74 242 16 $script:Muted $true
    Draw-Label $g 'DARK TASKBAR' 74 564 16 ([Drawing.ColorTranslator]::FromHtml('#AAB7CF')) $true
    $sampleSizes=@(16,24,32,64,256);$centers=@(130,290,450,650,1080)
    for($i=0;$i -lt $sampleSizes.Count;$i++){
        $size=$sampleSizes[$i];$frame=New-IconFrame $size
        $g.DrawImageUnscaled($frame,($centers[$i]-$size/2),(354-$size/2));$g.DrawImageUnscaled($frame,($centers[$i]-$size/2),(676-$size/2))
        Draw-Label $g "$size px" ($centers[$i]-25) 480 16 $script:Muted
        Draw-Label $g "$size px" ($centers[$i]-25) 802 16 ([Drawing.ColorTranslator]::FromHtml('#AAB7CF'))
        $frame.Dispose()
    }
    Draw-Label $g 'Actual pixel sizes shown above. Every ICO frame is drawn separately from the vector.' 52 881 20 $script:Muted
    $b.Save($PreviewPath,[Drawing.Imaging.ImageFormat]::Png)
    $lp.Dispose();$dp.Dispose();$light.Dispose();$dark.Dispose();$g.Dispose();$b.Dispose()
}
Write-Host 'Cobalt HardTop branding generated: 9 icon sizes, installer rail and social artwork.'
