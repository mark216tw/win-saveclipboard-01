namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 判斷視窗是否適合作為自動貼上的目標：必須是其他程式、可見，且不是系統匣／桌面等沒有輸入位置的地方。
/// </summary>
internal static class PasteTargetRules
{
    private static readonly HashSet<string> NonPasteTargetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "Progman",
        "WorkerW",
    };

    public static bool IsValidTarget(IntPtr hwnd, IntPtr self)
    {
        if (hwnd == IntPtr.Zero
            || hwnd == self
            || !NativeMethods.IsWindow(hwnd)
            || !NativeMethods.IsWindowVisible(hwnd))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == (uint)Environment.ProcessId)
        {
            return false;
        }

        var className = new System.Text.StringBuilder(64);
        int length = NativeMethods.GetClassNameW(hwnd, className, className.Capacity);
        return length == 0 || !NonPasteTargetClasses.Contains(className.ToString(0, length));
    }
}
