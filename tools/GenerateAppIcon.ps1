param(
    [string]$Destination = (Join-Path $PSScriptRoot "..\src\NipkgCreator\Assets\NipkgCreator.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectangle([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$Size) {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $backgroundPath = $null
    $backgroundBrush = $null
    $font = $null
    $whiteBrush = $null
    $gearBrush = $null
    $gearBorder = $null
    $gearHole = $null
    $stream = $null
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.Clear([System.Drawing.Color]::Transparent)
        $scale = [single]($Size / 256.0)
        $g.ScaleTransform($scale, $scale)

        $backgroundPath = New-RoundedRectangle 10 10 236 236 42
        $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.Rectangle]::new(10, 10, 236, 236),
            [System.Drawing.Color]::FromArgb(3, 108, 222),
            [System.Drawing.Color]::FromArgb(11, 49, 124),
            [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
        $g.FillPath($backgroundBrush, $backgroundPath)

        $font = [System.Drawing.Font]::new("Segoe UI", [single]116, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        $g.DrawString("NI", $font, $whiteBrush, [System.Drawing.PointF]::new(25, 42))

        $points = [System.Drawing.PointF[]]::new(32)
        for ($i = 0; $i -lt 32; $i++) {
            $angle = $i * [Math]::PI / 16.0 - [Math]::PI / 2.0
            $radius = if (($i % 4) -eq 0 -or ($i % 4) -eq 1) { 55.0 } else { 43.0 }
            $points[$i] = [System.Drawing.PointF]::new(
                [single](182 + [Math]::Cos($angle) * $radius),
                [single](182 + [Math]::Sin($angle) * $radius))
        }
        $gearBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(48, 208, 66))
        $gearBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(12, 116, 46), [single]3)
        $gearHole = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(11, 68, 117))
        $g.FillPolygon($gearBrush, $points)
        $g.DrawPolygon($gearBorder, $points)
        $g.FillEllipse($gearHole, 165, 165, 34, 34)

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        if ($stream) { $stream.Dispose() }
        if ($gearHole) { $gearHole.Dispose() }
        if ($gearBorder) { $gearBorder.Dispose() }
        if ($gearBrush) { $gearBrush.Dispose() }
        if ($whiteBrush) { $whiteBrush.Dispose() }
        if ($font) { $font.Dispose() }
        if ($backgroundBrush) { $backgroundBrush.Dispose() }
        if ($backgroundPath) { $backgroundPath.Dispose() }
        $g.Dispose()
        $bitmap.Dispose()
    }
}

$allSizes = @(16, 24, 32, 48, 64, 128, 256)
$allPng = @()
foreach ($size in $allSizes) {
    $allPng += ,(New-IconPng -Size $size)
}

$directory = Split-Path -Parent $Destination
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($Destination))
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$allSizes.Count)
    $offset = 6 + $allSizes.Count * 16
    for ($i = 0; $i -lt $allSizes.Count; $i++) {
        $dimension = if ($allSizes[$i] -eq 256) { 0 } else { $allSizes[$i] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$allPng[$i].Length)
        $writer.Write([UInt32]$offset)
        $offset += $allPng[$i].Length
    }
    foreach ($png in $allPng) {
        $writer.Write([byte[]]$png)
    }
}
finally {
    $writer.Dispose()
}
Write-Host "Generated multi-resolution EXE icon: $Destination"
