using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 選取即貼上的流程：記住面板開啟前的目標視窗，複製後切回該視窗並送出 Ctrl+V。
/// 所有 Win32 前景與輸入處理集中在這裡，讓視窗只負責顯示／隱藏。
/// </summary>
public sealed class PasteAutomationService
{
    private const int TargetActivationDelayMs = 140;
    private const int TargetSettleDelayMs = 90;

    private readonly Func<IntPtr> _getSelf;
    private readonly ForegroundWindowTracker _foregroundTracker;
    private readonly Func<bool> _pasteOnSelect;
    private readonly Func<bool> _keepPanelVisible;
    private readonly Func<bool> _isExiting;
    private readonly Action _hidePanel;
    private readonly Action<string>? _notify;
    private readonly CancellationToken _lifetimeToken;
    private readonly ILogger _logger;

    private IntPtr _pasteTarget;

    public PasteAutomationService(
        Func<IntPtr> getSelf,
        ForegroundWindowTracker foregroundTracker,
        Func<bool> pasteOnSelect,
        Func<bool> keepPanelVisible,
        Func<bool> isExiting,
        Action hidePanel,
        Action<string>? notify,
        CancellationToken lifetimeToken,
        ILogger logger)
    {
        _getSelf = getSelf;
        _foregroundTracker = foregroundTracker;
        _pasteOnSelect = pasteOnSelect;
        _keepPanelVisible = keepPanelVisible;
        _isExiting = isExiting;
        _hidePanel = hidePanel;
        _notify = notify;
        _lifetimeToken = lifetimeToken;
        _logger = logger;
    }

    public void CaptureTarget()
    {
        IntPtr self = _getSelf();
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (!PasteTargetRules.IsValidTarget(foreground, self))
        {
            // 點工作列或系統匣開啟時前景可能是殼層，改用追蹤到的最後一個外部前景
            foreground = _foregroundTracker.LastExternalForeground;
        }

        _pasteTarget = foreground;
        _logger.LogInformation(
            "貼上目標：{Target}",
            _pasteTarget == IntPtr.Zero ? "無" : $"0x{_pasteTarget.ToInt64():X}");
    }

    public void UpdateTargetFromForeground()
    {
        IntPtr self = _getSelf();
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (!PasteTargetRules.IsValidTarget(foreground, self))
        {
            foreground = _foregroundTracker.LastExternalForeground;
        }

        if (PasteTargetRules.IsValidTarget(foreground, self))
        {
            _pasteTarget = foreground;
            _logger.LogDebug("更新貼上目標：0x{Target}", foreground.ToInt64());
        }
    }

    public void ClearTarget() => _pasteTarget = IntPtr.Zero;

    public async Task PasteAsync(ClipboardEntry entry)
    {
        if (!_pasteOnSelect()
            || _isExiting()
            || _lifetimeToken.IsCancellationRequested)
        {
            return;
        }

        IntPtr self = _getSelf();
        IntPtr storedTarget = _pasteTarget;
        bool keepVisible = _keepPanelVisible();
        IntPtr target;

        if (keepVisible)
        {
            // 面板保持顯示在最上層時前景仍是面板本身，直接使用先前記錄的前一個視窗。
            target = ResolveTarget(storedTarget, self);
        }
        else
        {
            // 先隱藏面板，讓焦點自然回到開啟面板前的視窗，再以實際前景視窗確認貼上目標。
            _hidePanel();
            await Task.Delay(TargetActivationDelayMs, _lifetimeToken);
            target = ResolveTarget(storedTarget, self);
        }

        if (target == IntPtr.Zero)
        {
            _pasteTarget = IntPtr.Zero;
            _logger.LogDebug("沒有可自動貼上的目標視窗，維持僅複製。");
            return;
        }

        if (NativeMethods.GetForegroundWindow() != target)
        {
            ActivateForegroundWindow(target);
            await Task.Delay(TargetSettleDelayMs, _lifetimeToken);
        }

        if (NativeMethods.GetForegroundWindow() != target)
        {
            _pasteTarget = PasteTargetRules.IsValidTarget(target, self) ? target : IntPtr.Zero;
            _logger.LogInformation("貼上目標未能取得前景，改為僅複製。");
            _notify?.Invoke(AppStrings.Get("Paste_FailedForeground"));
            return;
        }

        if (!NativeMethods.SendPasteShortcut(out int errorCode))
        {
            _logger.LogWarning("送出 Ctrl+V 失敗（Win32 錯誤碼 {ErrorCode}），改為僅複製。", errorCode);
            _notify?.Invoke(AppStrings.Get("Paste_FailedInput"));
            return;
        }

        // 保留目標供連續貼上使用；視窗關閉後才清除
        _pasteTarget = NativeMethods.IsWindow(target) ? target : IntPtr.Zero;
        _logger.LogInformation(
            "已自動貼上至前一個視窗（{Kind}，目標 0x{Target}，面板保持顯示={KeepVisible}）。",
            entry.Kind,
            target.ToInt64(),
            keepVisible);
    }

    private IntPtr ResolveTarget(IntPtr storedTarget, IntPtr self)
    {
        IntPtr target = NativeMethods.GetForegroundWindow();
        if (!PasteTargetRules.IsValidTarget(target, self))
        {
            // 工作列／系統匣會讓前景短暫變成殼層，改用追蹤到的最後一個外部前景
            target = _foregroundTracker.LastExternalForeground;
        }

        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target) || !NativeMethods.IsWindowVisible(target))
        {
            target = storedTarget;
        }

        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target) || !NativeMethods.IsWindowVisible(target))
        {
            return IntPtr.Zero;
        }

        return target;
    }

    private static void ActivateForegroundWindow(IntPtr hwnd)
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        uint foregroundThread = foreground == IntPtr.Zero
            ? 0
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();
        bool attached = false;

        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attached = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.SetFocus(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}
