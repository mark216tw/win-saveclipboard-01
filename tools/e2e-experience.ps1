Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class PanelWin {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowW(string cls, string name);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
}
"@

$exe = Resolve-Path "src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe"
$settingsPath = Join-Path $env:APPDATA 'SaveClipboard\settings.json'
$backupPath = Join-Path $env:TEMP ('saveclipboard-settings-' + [guid]::NewGuid().ToString('N') + '.json')

function Get-PanelElement {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'SaveClipboard')
    $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}

function Get-ListItemNames($panel) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $panel.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $names = @()
    foreach ($item in $items) {
        $name = $item.Current.Name
        if ($name -and ($names -notcontains $name)) { $names += $name }
    }
    return $names
}

function Find-ByAutomationId($panel, $automationId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
    $panel.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

if (Test-Path $settingsPath) { Copy-Item $settingsPath $backupPath -Force }

$failed = $false
try {
    Get-Process SaveClipboard -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $tagAlpha = "Sprint2-Alpha-$suffix"
    $tagBeta = "Sprint2-Beta-$suffix"

    Set-Clipboard -Value 'warmup'
    $p = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3
    Set-Clipboard -Value $tagAlpha
    Start-Sleep -Milliseconds 900
    Set-Clipboard -Value $tagBeta
    Start-Sleep -Milliseconds 900

    $p2 = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3

    $panel = Get-PanelElement
    if ($null -eq $panel) {
        Write-Host 'PANEL NOT FOUND'
        $failed = $true
        throw 'panel missing'
    }

    $search = Find-ByAutomationId $panel 'SearchBox'
    if ($null -eq $search) {
        Write-Host 'SEARCH BOX NOT FOUND'
        $failed = $true
        throw 'search missing'
    }

    # --- search filter ---
    $valuePattern = $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $valuePattern.SetValue($tagAlpha)
    Start-Sleep -Milliseconds 800

    $names = Get-ListItemNames $panel
    Write-Host ("filtered items: {0}" -f $names.Count)
    foreach ($name in $names) { Write-Host (" - {0}" -f $name) }

    $alphaVisible = @($names | Where-Object { $_ -like "*$tagAlpha*" }).Count -gt 0
    $betaVisible = @($names | Where-Object { $_ -like "*$tagBeta*" }).Count -gt 0
    Write-Host ("alpha visible: {0}" -f $alphaVisible)
    Write-Host ("beta visible: {0}" -f $betaVisible)
    if (-not $alphaVisible -or $betaVisible) {
        Write-Host 'SEARCH FILTER FAIL'
        $failed = $true
    }

    # --- clearing the search restores the full list ---
    $valuePattern.SetValue('')
    Start-Sleep -Milliseconds 800
    $names = Get-ListItemNames $panel
    $alphaVisible = @($names | Where-Object { $_ -like "*$tagAlpha*" }).Count -gt 0
    $betaVisible = @($names | Where-Object { $_ -like "*$tagBeta*" }).Count -gt 0
    Write-Host ("after clear: items={0} alpha={1} beta={2}" -f $names.Count, $alphaVisible, $betaVisible)
    if (-not $alphaVisible -or -not $betaVisible) {
        Write-Host 'SEARCH CLEAR FAIL'
        $failed = $true
    }

    # --- pause / resume recording ---
    $pauseButton = Find-ByAutomationId $panel 'PauseButton'
    if ($null -eq $pauseButton) {
        Write-Host 'PAUSE BUTTON NOT FOUND'
        $failed = $true
    }
    else {
        $labelBefore = $pauseButton.Current.Name
        $pauseInvoke = $pauseButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $pauseInvoke.Invoke()
        Start-Sleep -Milliseconds 700
        $labelAfter = $pauseButton.Current.Name
        Write-Host ("pause label changed: {0}" -f ($labelBefore -ne $labelAfter))
        if ($labelBefore -eq $labelAfter) {
            Write-Host 'PAUSE LABEL FAIL'
            $failed = $true
        }

        $tagPaused = "Sprint2-Paused-$suffix"
        Set-Clipboard -Value $tagPaused
        Start-Sleep -Milliseconds 1200
        $names = Get-ListItemNames $panel
        $pausedCaptured = @($names | Where-Object { $_ -like "*$tagPaused*" }).Count -gt 0
        Write-Host ("paused capture: {0}" -f $pausedCaptured)
        if ($pausedCaptured) {
            Write-Host 'PAUSE RECORDING FAIL'
            $failed = $true
        }

        $pauseInvoke.Invoke()
        Start-Sleep -Milliseconds 700

        $tagResumed = "Sprint2-Resumed-$suffix"
        Set-Clipboard -Value $tagResumed
        Start-Sleep -Milliseconds 1200
        $names = Get-ListItemNames $panel
        $resumedCaptured = @($names | Where-Object { $_ -like "*$tagResumed*" }).Count -gt 0
        Write-Host ("resumed capture: {0}" -f $resumedCaptured)
        if (-not $resumedCaptured) {
            Write-Host 'RESUME RECORDING FAIL'
            $failed = $true
        }
    }

    # --- panel bounds memory ---
    $hwnd = [PanelWin]::FindWindowW('WinUIDesktopWin32WindowClass', 'SaveClipboard')
    if ($hwnd -eq [IntPtr]::Zero) {
        Write-Host 'PANEL HWND NOT FOUND'
        $failed = $true
        throw 'hwnd missing'
    }

    [PanelWin]::MoveWindow($hwnd, 200, 150, 640, 520, $true) | Out-Null
    Start-Sleep -Milliseconds 900

    $closeButton = Find-ByAutomationId $panel 'ClosePanelButton'
    if ($null -eq $closeButton) {
        Write-Host 'CLOSE BUTTON NOT FOUND'
        $failed = $true
        throw 'close missing'
    }

    $invoke = $closeButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    Start-Sleep -Milliseconds 1500

    if (-not (Test-Path $settingsPath)) {
        Write-Host 'SETTINGS FILE MISSING'
        $failed = $true
    }
    else {
        $saved = Get-Content $settingsPath -Raw | ConvertFrom-Json
        Write-Host ("saved bounds: has={0} {1},{2} {3}x{4}" -f $saved.HasPanelBounds, $saved.PanelLeft, $saved.PanelTop, $saved.PanelWidth, $saved.PanelHeight)
        if (-not $saved.HasPanelBounds -or $saved.PanelWidth -lt 600 -or $saved.PanelHeight -lt 480) {
            Write-Host 'BOUNDS SAVE FAIL'
            $failed = $true
        }
    }

    # --- restore on next launch ---
    Get-Process SaveClipboard -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800

    $p3 = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3
    $hwnd = [PanelWin]::FindWindowW('WinUIDesktopWin32WindowClass', 'SaveClipboard')
    $rect = New-Object PanelWin+RECT
    [PanelWin]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    Write-Host ("restored bounds: {0},{1} {2}x{3}" -f $rect.Left, $rect.Top, $width, $height)

    if ([Math]::Abs($width - 640) -gt 6 -or [Math]::Abs($height - 520) -gt 6) {
        Write-Host 'BOUNDS RESTORE FAIL'
        $failed = $true
    }

    Get-Process SaveClipboard -ErrorAction SilentlyContinue | Stop-Process -Force
}
finally {
    Get-Process SaveClipboard -ErrorAction SilentlyContinue | Stop-Process -Force
    if (Test-Path $backupPath) {
        Copy-Item $backupPath $settingsPath -Force
        Remove-Item $backupPath -Force
    }
    else {
        Remove-Item $settingsPath -ErrorAction SilentlyContinue
    }
}

if ($failed) {
    Write-Host 'EXPERIENCE E2E FAIL'
    exit 1
}

Write-Host 'EXPERIENCE E2E PASS'
exit 0
