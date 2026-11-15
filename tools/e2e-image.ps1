Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$exe = Resolve-Path "src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe"
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 2

$bmp = New-Object System.Drawing.Bitmap(120, 80)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::CornflowerBlue)
$g.FillEllipse([System.Drawing.Brushes]::Orange, 10, 10, 100, 60)
$g.Dispose()
[System.Windows.Forms.Clipboard]::SetImage($bmp)
Start-Sleep -Seconds 2
Stop-Process -Id $p.Id -Force
Write-Host 'image placed on clipboard via WinForms Clipboard.SetImage'
