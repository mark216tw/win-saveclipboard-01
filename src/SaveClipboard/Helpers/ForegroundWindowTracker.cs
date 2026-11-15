using System.Runtime.InteropServices;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 持續記錄最後一個可貼上的外部前景視窗。
/// 點擊工作列或系統匣時前景會暫時變成殼層，單靠當下的 GetForegroundWindow 會拿不到真正的前一個視窗。
/// 事件以 WINEVENT_OUTOFCONTEXT 送回安裝執行緒（UI 執行緒），因此欄位只在該執行緒讀寫。
/// </summary>
public sealed class ForegroundWindowTracker : IDisposable
{
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly IntPtr _hook;
    private IntPtr _lastExternalForeground;
    private bool _disposed;

    public ForegroundWindowTracker()
    {
        _callback = OnForegroundChanged;
        _lastExternalForeground = GetCurrentCandidate();
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
    }

    /// <summary>最後一個通過 <see cref="PasteTargetRules"/> 檢查的外部前景視窗；沒有時為 <see cref="IntPtr.Zero"/>。</summary>
    public IntPtr LastExternalForeground => _lastExternalForeground;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
        }

        // 回呼委派必須存活到 hook 卸除後，避免原生端持有已回收的函式指標
        GC.KeepAlive(_callback);
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (_disposed || eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND)
        {
            return;
        }

        if (PasteTargetRules.IsValidTarget(hwnd, IntPtr.Zero))
        {
            _lastExternalForeground = hwnd;
        }
    }

    private static IntPtr GetCurrentCandidate()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        return PasteTargetRules.IsValidTarget(foreground, IntPtr.Zero) ? foreground : IntPtr.Zero;
    }
}
