using Com.VibeCoding.SaveClipboard.Helpers;
using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 系統列圖示（Shell_NotifyIcon）。左鍵開啟面板，右鍵顯示 開啟／結束 選單。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const uint TrayIconId = 1;
    private const uint MenuOpen = 1001;
    private const uint MenuExit = 1002;
    private const uint MenuPause = 1003;
    private const int TrayActionDebounceMs = 300;

    private readonly HelperWindow _window;
    private readonly Action _openPanel;
    private readonly Action _togglePause;
    private readonly Func<bool> _isPaused;
    private readonly Action _exit;
    private readonly ILogger<TrayIconService> _logger;
    private readonly uint _taskbarCreatedMessage;
    private IntPtr _iconHandle;
    private bool _added;
    private bool _disposed;
    private bool _version4;
    private DateTimeOffset _lastOpenActionAt;
    private DateTimeOffset _lastContextActionAt;

    public TrayIconService(
        HelperWindow window,
        string iconPath,
        Action openPanel,
        Action togglePause,
        Func<bool> isPaused,
        Action exit,
        ILogger<TrayIconService> logger)
    {
        _window = window;
        _openPanel = openPanel;
        _togglePause = togglePause;
        _isPaused = isPaused;
        _exit = exit;
        _logger = logger;
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
        if (_taskbarCreatedMessage == 0)
        {
            _logger.LogWarning("無法註冊 TaskbarCreated 訊息，Explorer 重新啟動後系統列圖示不會自動復原。");
        }

        int iconSize = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        _iconHandle = NativeMethods.LoadImageW(IntPtr.Zero, iconPath, NativeMethods.IMAGE_ICON, iconSize, iconSize, NativeMethods.LR_LOADFROMFILE);
        if (_iconHandle == IntPtr.Zero)
        {
            _logger.LogWarning("無法載入系統列圖示：{Path}", iconPath);
        }

        _window.MessageReceived += OnWindowMessage;
        AddIcon();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.MessageReceived -= OnWindowMessage;
        RemoveIcon();

        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }

    /// <summary>暫停狀態改變時更新工具提示，讓使用者不必開啟面板也能確認目前狀態。</summary>
    public void UpdatePausedState()
    {
        if (_disposed || !_added)
        {
            return;
        }

        NativeMethods.NOTIFYICONDATAW data = CreateNotifyIconData();
        data.uFlags = NativeMethods.NIF_TIP;
        if (!NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref data))
        {
            _logger.LogWarning("Shell_NotifyIconW(NIM_MODIFY) 更新工具提示失敗。");
        }
    }

    /// <summary>取得系統列圖示目前所在的螢幕座標；失敗時回傳 false。</summary>
    internal bool TryGetIconRect(out NativeMethods.RECT rect)
    {
        rect = default;
        if (_disposed || !_added)
        {
            return false;
        }

        var identifier = new NativeMethods.NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONIDENTIFIER>(),
            hWnd = _window.Handle,
            uID = TrayIconId,
        };

        return NativeMethods.Shell_NotifyIconGetRect(ref identifier, out rect) == 0;
    }

    /// <summary>顯示系統列氣泡提示，用於面板關閉後仍需要告知使用者的訊息。</summary>
    public void ShowBalloon(string title, string message, bool warning = false)
    {
        if (_disposed || !_added)
        {
            return;
        }

        NativeMethods.NOTIFYICONDATAW data = CreateNotifyIconData();
        data.uFlags = NativeMethods.NIF_INFO;
        data.szInfoTitle = title;
        data.szInfo = message;
        data.dwInfoFlags = warning ? NativeMethods.NIIF_WARNING : NativeMethods.NIIF_INFO;

        if (!NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref data))
        {
            _logger.LogWarning("Shell_NotifyIconW(NIM_MODIFY) 顯示氣泡提示失敗。");
        }
    }

    private void AddIcon()
    {
        NativeMethods.NOTIFYICONDATAW data = CreateNotifyIconData();
        if (NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref data))
        {
            data.uVersionOrTimeout = NativeMethods.NOTIFYICON_VERSION_4;
            data.uFlags = 0;
            _version4 = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_SETVERSION, ref data);
            _added = true;
        }
        else
        {
            _logger.LogError("Shell_NotifyIconW(NIM_ADD) 失敗。");
        }
    }

    private void RemoveIcon()
    {
        if (!_added)
        {
            return;
        }

        NativeMethods.NOTIFYICONDATAW data = CreateNotifyIconData();
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref data);
        _added = false;
    }

    private NativeMethods.NOTIFYICONDATAW CreateNotifyIconData() => new()
    {
        cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATAW>(),
        hWnd = _window.Handle,
        uID = TrayIconId,
        uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_TIP | NativeMethods.NIF_SHOWTIP | (_iconHandle != IntPtr.Zero ? NativeMethods.NIF_ICON : 0),
        uCallbackMessage = NativeMethods.WM_TRAYICON,
        hIcon = _iconHandle,
        szTip = _isPaused() ? AppStrings.Get("Tray_ToolTipPaused") : AppStrings.Get("Tray_ToolTipNormal"),
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private void OnWindowMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
        {
            // Explorer 重新啟動後系統列圖示會消失，必須重新加入
            _logger.LogInformation("偵測到工作列重新建立，重新加入系統列圖示。");
            _added = false;
            AddIcon();
            return;
        }

        if (msg != NativeMethods.WM_TRAYICON)
        {
            return;
        }

        // NOTIFYICON_VERSION_4：事件放在 LOWORD(lParam)，左鍵為 NIN_SELECT、右鍵為 WM_CONTEXTMENU。
        // 若 v4 設定失敗才退回舊式滑鼠訊息；同一種事件只處理一次，避免殼層同時送出多個通知而反覆開關。
        uint mouseMessage = unchecked((uint)(long)lParam) & 0xFFFF;

        bool isOpen = _version4
            ? mouseMessage is NativeMethods.NIN_SELECT or NativeMethods.NIN_KEYSELECT
            : mouseMessage is NativeMethods.WM_LBUTTONUP or NativeMethods.WM_LBUTTONDBLCLK;
        if (isOpen)
        {
            if (ShouldHandle(ref _lastOpenActionAt))
            {
                _openPanel();
            }

            return;
        }

        bool isContextMenu = _version4
            ? mouseMessage is NativeMethods.WM_CONTEXTMENU
            : mouseMessage is NativeMethods.WM_RBUTTONUP;
        if (isContextMenu && ShouldHandle(ref _lastContextActionAt))
        {
            ShowContextMenu();
        }
    }

    private static bool ShouldHandle(ref DateTimeOffset lastActionAt)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - lastActionAt < TimeSpan.FromMilliseconds(TrayActionDebounceMs))
        {
            return false;
        }

        lastActionAt = now;
        return true;
    }

    private void ShowContextMenu()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT point))
        {
            return;
        }

        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            bool paused = _isPaused();
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, (UIntPtr)MenuOpen, AppStrings.Get("Tray_Open"));
            NativeMethods.AppendMenuW(
                hMenu,
                NativeMethods.MF_STRING | (paused ? NativeMethods.MF_CHECKED : 0),
                (UIntPtr)MenuPause,
                paused ? AppStrings.Get("Tray_Resume") : AppStrings.Get("Tray_Pause"));
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, (UIntPtr)MenuExit, AppStrings.Get("Tray_Exit"));

            // 讓選單在點擊外部時能關閉：暫時顯示輔助視窗並取得前景，關閉後立即隱藏
            NativeMethods.ShowWindow(_window.Handle, NativeMethods.SW_SHOWNOACTIVATE);
            NativeMethods.SetForegroundWindow(_window.Handle);

            int command = NativeMethods.TrackPopupMenu(
                hMenu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
                point.X,
                point.Y,
                0,
                _window.Handle,
                IntPtr.Zero);

            NativeMethods.PostMessageW(_window.Handle, 0, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case (int)MenuOpen:
                    _openPanel();
                    break;
                case (int)MenuPause:
                    _togglePause();
                    break;
                case (int)MenuExit:
                    _exit();
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
            NativeMethods.ShowWindow(_window.Handle, NativeMethods.SW_HIDE);
        }
    }
}
