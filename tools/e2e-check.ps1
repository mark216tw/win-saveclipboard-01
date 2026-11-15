Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$exe = Resolve-Path "src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe"

function Get-PanelWindow {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'SaveClipboard')
    $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}

# 1) start first instance
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

# 2) put two distinctive texts on the clipboard
$tag1 = "SaveClipboardE2E-Alpha-$([guid]::NewGuid().ToString('N').Substring(0,8))"
$tag2 = "SaveClipboardE2E-Beta-$([guid]::NewGuid().ToString('N').Substring(0,8))"
Set-Clipboard -Value $tag1
Start-Sleep -Milliseconds 800
Set-Clipboard -Value $tag2
Start-Sleep -Milliseconds 1500

# 3) launch second instance -> should ask first to show the panel
$p2 = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

# 4) inspect panel content via UI Automation
$panel = Get-PanelWindow
if ($null -eq $panel) {
    Write-Host 'PANEL WINDOW NOT FOUND'
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "Panel found: $($panel.Current.Name)"

$all = $panel.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$texts = @()
foreach ($el in $all) {
    $name = $el.Current.Name
    if ($name -and ($name -like '*SaveClipboardE2E*') -and ($texts -notcontains $name)) {
        $texts += $name
    }
}
Write-Host ("Matched items in panel: {0}" -f $texts.Count)
foreach ($t in $texts) { Write-Host (" - {0}" -f $t) }

$alphaCaptured = @($texts | Where-Object { $_ -like "*$tag1*" }).Count -gt 0
$betaCaptured = @($texts | Where-Object { $_ -like "*$tag2*" }).Count -gt 0
Write-Host ("Alpha captured: {0}, Beta captured: {1}" -f $alphaCaptured, $betaCaptured)

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
if ($alphaCaptured -and $betaCaptured) { Write-Host 'E2E PASS'; exit 0 } else { Write-Host 'E2E FAIL'; exit 1 }
