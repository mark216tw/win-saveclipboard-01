using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 主面板的位置與大小計算：優先還原保存值，其次定位在系統列圖示附近。
/// 保存值會依目前螢幕 DPI 換算，並限制在工作區內，避免換螢幕或縮放後跑出畫面。
/// </summary>
public sealed class WindowPlacementService
{
    private readonly AppWindow _appWindow;
    private readonly Func<RectInt32?> _getTrayRect;
    private readonly ILogger<WindowPlacementService> _logger;

    public WindowPlacementService(
        AppWindow appWindow,
        Func<RectInt32?> getTrayRect,
        ILogger<WindowPlacementService> logger)
    {
        _appWindow = appWindow;
        _getTrayRect = getTrayRect;
        _logger = logger;
    }

    public void Apply(bool hasStoredBounds, RectInt32 storedBounds, int storedDpi)
    {
        if (hasStoredBounds && TryRestore(storedBounds, storedDpi))
        {
            return;
        }

        ApplyDefaultNearTray();
    }

    public static RectInt32 ScaleForDpi(RectInt32 bounds, int fromDpi, int toDpi)
    {
        int safeFrom = Math.Max(1, fromDpi);
        int safeTo = Math.Max(1, toDpi);
        int width = Math.Max(1, (int)Math.Round(bounds.Width * (double)safeTo / safeFrom));
        int height = Math.Max(1, (int)Math.Round(bounds.Height * (double)safeTo / safeFrom));
        return new RectInt32(bounds.X, bounds.Y, width, height);
    }

    internal static RectInt32 ClampToWorkArea(RectInt32 bounds, NativeMethods.RECT workArea)
    {
        int workWidth = Math.Max(1, workArea.Right - workArea.Left);
        int workHeight = Math.Max(1, workArea.Bottom - workArea.Top);
        int maxWidth = Math.Max(AppSettings.MinPanelWidth, Math.Min(AppSettings.MaxPanelWidth, workWidth));
        int maxHeight = Math.Max(AppSettings.MinPanelHeight, Math.Min(AppSettings.MaxPanelHeight, workHeight));
        int width = Math.Clamp(bounds.Width, AppSettings.MinPanelWidth, maxWidth);
        int height = Math.Clamp(bounds.Height, AppSettings.MinPanelHeight, maxHeight);

        int x = Math.Clamp(bounds.X, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        int y = Math.Clamp(bounds.Y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        return new RectInt32(x, y, width, height);
    }

    private bool TryRestore(RectInt32 stored, int storedDpi)
    {
        var rect = new NativeMethods.RECT
        {
            Left = stored.X,
            Top = stored.Y,
            Right = stored.X + stored.Width,
            Bottom = stored.Y + stored.Height,
        };

        IntPtr monitor = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (!TryGetMonitorInfo(monitor, out NativeMethods.MONITORINFO info))
        {
            return false;
        }

        int currentDpi = GetMonitorDpi(monitor);
        RectInt32 scaled = ScaleForDpi(stored, storedDpi, currentDpi);
        RectInt32 clamped = ClampToWorkArea(scaled, info.rcWork);
        _appWindow.MoveAndResize(clamped);
        _logger.LogInformation(
            "還原面板位置（DPI {StoredDpi}->{CurrentDpi}）",
            storedDpi,
            currentDpi);
        return true;
    }

    private void ApplyDefaultNearTray()
    {
        RectInt32? trayRect = _getTrayRect?.Invoke();
        if (trayRect is { } tray)
        {
            var trayNative = new NativeMethods.RECT
            {
                Left = tray.X,
                Top = tray.Y,
                Right = tray.X + tray.Width,
                Bottom = tray.Y + tray.Height,
            };
            IntPtr monitor = NativeMethods.MonitorFromRect(ref trayNative, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (TryGetMonitorInfo(monitor, out NativeMethods.MONITORINFO info))
            {
                PlaceNearTray(tray, info, GetMonitorDpi(monitor));
                return;
            }
        }

        ApplyDefaultNearCursor();
    }

    private void PlaceNearTray(RectInt32 tray, NativeMethods.MONITORINFO info, int dpi)
    {
        double scale = dpi / 96.0;
        int width = Math.Min((int)Math.Round(AppSettings.DefaultPanelWidth * scale), info.rcWork.Right - info.rcWork.Left);
        int height = Math.Min((int)Math.Round(AppSettings.DefaultPanelHeight * scale), info.rcWork.Bottom - info.rcWork.Top);
        int margin = (int)Math.Round(12 * scale);

        int x = Math.Clamp(tray.X + tray.Width - width, info.rcWork.Left, Math.Max(info.rcWork.Left, info.rcWork.Right - width));
        int y = Math.Clamp(tray.Y - height - margin, info.rcWork.Top, Math.Max(info.rcWork.Top, info.rcWork.Bottom - height));
        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void ApplyDefaultNearCursor()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT cursor))
        {
            return;
        }

        IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (!TryGetMonitorInfo(monitor, out NativeMethods.MONITORINFO info))
        {
            return;
        }

        int dpi = GetMonitorDpi(monitor);
        double scale = dpi / 96.0;
        int width = Math.Min((int)Math.Round(AppSettings.DefaultPanelWidth * scale), info.rcWork.Right - info.rcWork.Left);
        int height = Math.Min((int)Math.Round(AppSettings.DefaultPanelHeight * scale), info.rcWork.Bottom - info.rcWork.Top);
        int margin = (int)Math.Round(12 * scale);

        int x = Math.Max(info.rcWork.Right - width - margin, info.rcWork.Left);
        int y = Math.Max(info.rcWork.Bottom - height - margin, info.rcWork.Top);
        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private static bool TryGetMonitorInfo(IntPtr monitor, out NativeMethods.MONITORINFO info)
    {
        info = new NativeMethods.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>(),
        };
        return monitor != IntPtr.Zero && NativeMethods.GetMonitorInfoW(monitor, ref info);
    }

    private static int GetMonitorDpi(IntPtr monitor)
    {
        if (monitor != IntPtr.Zero
            && NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0
            && dpiX > 0)
        {
            return (int)dpiX;
        }

        return AppSettings.DefaultPanelDpi;
    }
}
