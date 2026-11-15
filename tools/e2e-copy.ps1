[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinClick {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string name);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    public const uint LEFTDOWN = 0x0002;
    public const uint LEFTUP = 0x0004;
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

$exe = Resolve-Path "src\SaveClipboard\bin\Debug\net10.0-windows10.0.19041.0\SaveClipboard.exe"
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'SaveClipboard')

$tag = "E2E-Copy-$([guid]::NewGuid().ToString('N').Substring(0,8))"
Set-Clipboard -Value 'placeholder-before'
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3
Set-Clipboard -Value $tag
Start-Sleep -Milliseconds 1200
$p2 = Start-Process $exe -PassThru
Start-Sleep -Seconds 2

$hwnd = [WinClick]::FindWindowW('WinUIDesktopWin32WindowClass', 'SaveClipboard')
Write-Host "hwnd=$hwnd"
# attach input queues so we can bring the panel to foreground
$pidOut = 0
$winThread = [WinClick]::GetWindowThreadProcessId($hwnd, [ref]$pidOut)
$ourThread = [WinClick]::GetCurrentThreadId()
[WinClick]::AttachThreadInput($ourThread, $winThread, $true) | Out-Null
[WinClick]::SetForegroundWindow($hwnd) | Out-Null
[WinClick]::AttachThreadInput($ourThread, $winThread, $false) | Out-Null
Start-Sleep -Milliseconds 400

$panel = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
$itemCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
$items = $panel.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCond)
Write-Host "items: $($items.Count)"
$rect = $items[0].Current.BoundingRectangle
$cx = [int]($rect.X + $rect.Width / 2)
$cy = [int]($rect.Y + 20)
Write-Host "clicking item at ($cx,$cy) - item: $($items[0].Current.Name)"

# clear clipboard to a known sentinel, then click the item and read it back
Set-Clipboard -Value 'sentinel-value'
Write-Host "clipboard before click: $(Get-Clipboard -Raw)"
[WinClick]::Click($cx, $cy)
Start-Sleep -Milliseconds 800
Write-Host "clipboard after click:  $(Get-Clipboard -Raw)"

Stop-Process -Id $p.Id -Force
