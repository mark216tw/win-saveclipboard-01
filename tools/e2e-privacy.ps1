Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class PrivacyClipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr memory);

    public static bool SetText(string text, string markerFormat, int markerValue)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                System.Threading.Thread.Sleep(30);
                continue;
            }

            try
            {
                EmptyClipboard();
                if (!SetTextData(text))
                {
                    return false;
                }

                if (markerFormat != null)
                {
                    SetDword(RegisterClipboardFormatW(markerFormat), unchecked((uint)markerValue));
                }

                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }

        return false;
    }

    private static bool SetTextData(string text)
    {
        int byteCount = (text.Length + 1) * 2;
        IntPtr handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        IntPtr pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(handle);
            return false;
        }

        try
        {
            char[] chars = text.ToCharArray();
            Marshal.Copy(chars, 0, pointer, chars.Length);
            Marshal.WriteInt16(pointer, text.Length * 2, 0);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        return SetClipboardData(CF_UNICODETEXT, handle) != IntPtr.Zero;
    }

    private static void SetDword(uint format, uint value)
    {
        IntPtr handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)4);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        IntPtr pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(handle);
            return;
        }

        try
        {
            Marshal.WriteInt32(pointer, unchecked((int)value));
        }
        finally
        {
            GlobalUnlock(handle);
        }

        if (SetClipboardData(format, handle) == IntPtr.Zero)
        {
            GlobalFree(handle);
        }
    }
}
"@

$exe = Resolve-Path "src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe"
$logPath = Join-Path $env:LOCALAPPDATA 'SaveClipboard\Logs\app.log'

function Get-PanelWindow {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'SaveClipboard')
    $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}

Get-Process SaveClipboard -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
$tagControl = "SaveClipboardPrivacy-Control-$suffix"
$tagExcluded = "SaveClipboardPrivacy-Excluded-$suffix"
$tagNoHistory = "SaveClipboardPrivacy-NoHistory-$suffix"

$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

[PrivacyClipboard]::SetText($tagControl, $null, 0) | Out-Null
Start-Sleep -Milliseconds 800
[PrivacyClipboard]::SetText($tagExcluded, 'ExcludeClipboardContentFromMonitorProcessing', 1) | Out-Null
Start-Sleep -Milliseconds 800
[PrivacyClipboard]::SetText($tagNoHistory, 'CanIncludeInClipboardHistory', 0) | Out-Null
Start-Sleep -Milliseconds 800

$p2 = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

$panel = Get-PanelWindow
if ($null -eq $panel) {
    Write-Host 'PANEL WINDOW NOT FOUND'
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

$all = $panel.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$matched = @()
foreach ($element in $all) {
    $name = $element.Current.Name
    if ($name -and ($name -like '*SaveClipboardPrivacy*') -and ($matched -notcontains $name)) {
        $matched += $name
    }
}

Write-Host ("Matched items: {0}" -f $matched.Count)
foreach ($name in $matched) { Write-Host (" - {0}" -f $name) }

$controlFound = $matched -contains $tagControl
$excludedFound = $matched -contains $tagExcluded
$noHistoryFound = $matched -contains $tagNoHistory

Write-Host ("control captured: {0}" -f $controlFound)
Write-Host ("excluded captured: {0}" -f $excludedFound)
Write-Host ("no-history captured: {0}" -f $noHistoryFound)

if (Test-Path $logPath) {
    $excludedSkips = Select-String -Path $logPath -Pattern 'ExcludedBySource' -SimpleMatch
    $noHistorySkips = Select-String -Path $logPath -Pattern 'HistoryDisabledBySource' -SimpleMatch
    Write-Host ("skip log lines: excluded={0} no-history={1}" -f $excludedSkips.Count, $noHistorySkips.Count)
}

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue

if ($controlFound -and (-not $excludedFound) -and (-not $noHistoryFound)) {
    Write-Host 'PRIVACY E2E PASS'
    exit 0
}

Write-Host 'PRIVACY E2E FAIL'
exit 1
