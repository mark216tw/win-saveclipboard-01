# Generates SaveClipboard app icon assets from the 1024x1024 master artwork (tools\AppIcon-1024.png)
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\generate-icon.ps1
# Output: src\SaveClipboard\Assets\AppIcon.ico (16/24/32/48/64/128/256) and AppIcon-256.png

param(
    [string]$SourcePath = (Join-Path $PSScriptRoot 'AppIcon-1024.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source icon master not found: $SourcePath"
}

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\SaveClipboard\Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

function Resize-Icon([System.Drawing.Image]$source, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $attrs = New-Object System.Drawing.Imaging.ImageAttributes
    $attrs.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $g.DrawImage($source, $destRect, 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attrs)

    $attrs.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBytes = @{}

$sourceImage = [System.Drawing.Image]::FromFile($SourcePath)
try {
    foreach ($size in $sizes) {
        $bmp = Resize-Icon $sourceImage $size
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes[$size] = $ms.ToArray()
        if ($size -eq 256) {
            [System.IO.File]::WriteAllBytes((Join-Path $assets 'AppIcon-256.png'), $pngBytes[$size])
        }
        $ms.Dispose()
        $bmp.Dispose()
    }
}
finally {
    $sourceImage.Dispose()
}

# Pack ICO (PNG-compressed entries, Vista+)
$icoPath = Join-Path $assets 'AppIcon.ico'
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)          # reserved
$bw.Write([uint16]1)          # type: icon
$bw.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
foreach ($size in $sizes) {
    $data = $pngBytes[$size]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $bw.Write([byte]$dim)     # width (0 = 256)
    $bw.Write([byte]$dim)     # height
    $bw.Write([byte]0)        # colors
    $bw.Write([byte]0)        # reserved
    $bw.Write([uint16]1)      # planes
    $bw.Write([uint16]32)     # bpp
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($size in $sizes) {
    $bw.Write($pngBytes[$size])
}
$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$bw.Dispose()
$out.Dispose()

Write-Host "Generated $icoPath and AppIcon-256.png from $SourcePath"
