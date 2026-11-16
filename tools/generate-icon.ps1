# Generates the SaveClipboard app icon (transparent clipboard with a centered lightning bolt, forest green and white)
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\generate-icon.ps1
# Output: src\SaveClipboard\Assets\AppIcon.ico (16/24/32/48/64/128/256) and AppIcon-256.png

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\SaveClipboard\Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

$Green = [System.Drawing.Color]::FromArgb(255, 46, 107, 58)     # forest green
$White = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)  # paper and clip

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function DrawIcon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 256.0
    $greenBrush = New-Object System.Drawing.SolidBrush($Green)
    $whiteBrush = New-Object System.Drawing.SolidBrush($White)

    # clipboard body (forest green)
    $body = New-RoundedRectPath (32 * $s) (24 * $s) (192 * $s) (208 * $s) (34 * $s)
    $g.FillPath($greenBrush, $body)
    $body.Dispose()

    # paper (white)
    $paper = New-RoundedRectPath (50 * $s) (64 * $s) (156 * $s) (148 * $s) (20 * $s)
    $g.FillPath($whiteBrush, $paper)
    $paper.Dispose()

    # clip (white)
    $clip = New-RoundedRectPath (100 * $s) (34 * $s) (56 * $s) (32 * $s) (12 * $s)
    $g.FillPath($whiteBrush, $clip)
    $clip.Dispose()

    # clip slot (skip at 16/24 px where it would blur)
    if ($size -ge 32) {
        $slot = New-RoundedRectPath (112 * $s) (43 * $s) (32 * $s) (8 * $s) (4 * $s)
        $g.FillPath($greenBrush, $slot)
        $slot.Dispose()
    }

    # centered lightning bolt (wider shape at 16/24 px so the zigzag survives)
    if ($size -le 24) {
        $bolt = @(
            @(150, 82),
            @(98, 146),
            @(122, 146),
            @(104, 198),
            @(158, 130),
            @(134, 130)
        )
        $boltScale = if ($size -le 16) { 1.12 } else { 1.06 }
    }
    else {
        $bolt = @(
            @(146, 84),
            @(106, 144),
            @(124, 144),
            @(112, 194),
            @(152, 132),
            @(134, 132)
        )
        $boltScale = 1.0
    }
    $cx = 128.0
    $cy = 139.0
    $points = foreach ($p in $bolt) {
        $x = $cx + ($p[0] - $cx) * $boltScale
        $y = $cy + ($p[1] - $cy) * $boltScale
        New-Object System.Drawing.PointF(($x * $s), ($y * $s))
    }
    $g.FillPolygon($greenBrush, [System.Drawing.PointF[]]$points)

    $greenBrush.Dispose()
    $whiteBrush.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBytes = @{}
foreach ($size in $sizes) {
    $bmp = DrawIcon $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes[$size] = $ms.ToArray()
    if ($size -eq 256) {
        [System.IO.File]::WriteAllBytes((Join-Path $assets 'AppIcon-256.png'), $pngBytes[$size])
    }
    $ms.Dispose()
    $bmp.Dispose()
}

# pack ICO (PNG-compressed entries, Vista+)
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

Write-Host "Generated $icoPath and AppIcon-256.png"
