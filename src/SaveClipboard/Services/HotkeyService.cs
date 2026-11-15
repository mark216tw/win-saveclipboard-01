using Com.VibeCoding.SaveClipboard.Helpers;
using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 全域快捷鍵（預設 Ctrl+Alt+V），按下時切換剪貼簿面板顯示／隱藏。
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5343;
    private const string ShortcutText = "Ctrl+Alt+V";

    private readonly HelperWindow _window;
    private readonly Action _togglePanel;
    private readonly ILogger<HotkeyService> _logger;
    private bool _disposed;

    public HotkeyService(HelperWindow window, Action togglePanel, ILogger<HotkeyService> logger)
    {
        _window = window;
        _togglePanel = togglePanel;
        _logger = logger;

        _window.MessageReceived += OnWindowMessage;
        IsRegistered = NativeMethods.RegisterHotKey(
            _window.Handle,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_V);

        if (!IsRegistered)
        {
            _logger.LogWarning("註冊全域快捷鍵 {Shortcut} 失敗，可能已被其他程式使用。", ShortcutText);
        }
    }

    public bool IsRegistered { get; }

    public static string DisplayText => ShortcutText;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.MessageReceived -= OnWindowMessage;

        if (IsRegistered)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, HotkeyId);
        }
    }

    private void OnWindowMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && (int)wParam == HotkeyId)
        {
            _togglePanel();
        }
    }
}
