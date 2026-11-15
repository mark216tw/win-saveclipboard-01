# Generates the SaveClipboard app icon (flat, yellow rounded-square background, two documents, simple lines)
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\generate-icon.ps1
# Output: src\SaveClipboard\Assets\AppIcon.ico (16/24/32/48/64/128/256) and AppIcon-256.png

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\SaveClipboard\Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

$BackgroundColor = [System.Drawing.Color]::FromArgb(255, 255, 193, 7)  # amber yellow
$BackColor  = [System.Drawing.Color]::FromArgb(255, 91, 132, 181)   # slate blue
$PaperColor = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)  # white
$LineColor  = [System.Drawing.Color]::FromArgb(255, 47, 75, 124)    # deep blue lines
$Accent     = [System.Drawing.Color]::FromArgb(255, 242, 161, 4)    # amber title line

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
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 256.0

    # yellow rounded-square background
    $background = New-RoundedRectPath 0 0 $size $size (56 * $s)
    $g.FillPath((New-Object System.Drawing.SolidBrush $BackgroundColor), $background)
    $background.Dispose()

    # back document (slate blue)
    $back = New-RoundedRectPath (36 * $s) (64 * $s) (152 * $s) (158 * $s) (26 * $s)
    $g.FillPath((New-Object System.Drawing.SolidBrush $BackColor), $back)
    $back.Dispose()

    # front document (white, no folded corner)
    $fx = 68 * $s; $fy = 28 * $s; $fw = 152 * $s; $fh = 176 * $s; $fr = 26 * $s
    $front = New-RoundedRectPath $fx $fy $fw $fh $fr
    $g.FillPath((New-Object System.Drawing.SolidBrush $PaperColor), $front)
    $borderPen = New-Object System.Drawing.Pen($LineColor, [Math]::Max(1.0, 4 * $s))
    $g.DrawPath($borderPen, $front)
    $borderPen.Dispose()
    $front.Dispose()

    # text lines: amber title line + two deep blue body lines
    $lineX = $fx + 28 * $s
    $lineH = [Math]::Max(2, [int](12 * $s))
    $lines = @(
        @{ y = 94;  w = 60;  color = $Accent },
        @{ y = 132; w = 104; color = $LineColor },
        @{ y = 166; w = 84;  color = $LineColor }
    )
    foreach ($line in $lines) {
        $rectY = $line.y * $s
        $path = New-RoundedRectPath $lineX $rectY ($line.w * $s) $lineH ($lineH / 2)
        $g.FillPath((New-Object System.Drawing.SolidBrush $line.color), $path)
        $path.Dispose()
    }

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
