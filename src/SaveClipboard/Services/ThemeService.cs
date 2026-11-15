using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 依設定即時套用顯示模式與主題色。直接修改共用筆刷的 Color，
/// 讓既有 ThemeResource 參照即時更新；高對比模式改用系統色彩。
/// </summary>
public sealed class ThemeService : IDisposable
{
    private readonly ILogger<ThemeService> _logger;
    private FrameworkElement? _root;
    private AccessibilitySettings? _accessibility;
    private UISettings? _uiSettings;
    private DisplayMode _displayMode = DisplayMode.System;
    private string _themeId = ThemeCatalog.DefaultThemeId;
    private bool _applying;
    private bool _disposed;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    public bool IsDark { get; private set; }

    public bool IsHighContrast { get; private set; }

    public void Attach(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (_root is not null)
        {
            _root.ActualThemeChanged -= OnActualThemeChanged;
        }

        _root = root;
        _root.ActualThemeChanged += OnActualThemeChanged;
        EnsureAccessibilityListeners();
        Apply(_displayMode, _themeId);
    }

    public void Apply(DisplayMode displayMode, string? themeId)
    {
        _displayMode = Enum.IsDefined(displayMode) ? displayMode : DisplayMode.System;
        _themeId = ThemeCatalog.Get(themeId).Id;

        if (_root is null)
        {
            return;
        }

        _applying = true;
        try
        {
            _root.RequestedTheme = _displayMode switch
            {
                DisplayMode.Light => ElementTheme.Light,
                DisplayMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            ApplyBrushes();
        }
        finally
        {
            _applying = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_root is not null)
        {
            _root.ActualThemeChanged -= OnActualThemeChanged;
            _root = null;
        }

        if (_uiSettings is not null)
        {
            _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
            _uiSettings = null;
        }

        _accessibility = null;
    }

    private void EnsureAccessibilityListeners()
    {
        if (_uiSettings is not null)
        {
            return;
        }

        try
        {
            _accessibility = new AccessibilitySettings();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "無法讀取高對比設定。");
            _accessibility = null;
        }

        try
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "無法監聽系統色彩變化。");
            _uiSettings = null;
        }
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_applying)
        {
            return;
        }

        ApplyBrushes();
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        ApplyBrushes();
    }

    private void ApplyBrushes()
    {
        if (_root is null)
        {
            return;
        }

        if (IsHighContrastEnabled())
        {
            ApplyHighContrastBrushes();
            return;
        }

        IsHighContrast = false;
        ThemeOption theme = ThemeCatalog.Get(_themeId);
        bool isDark = _root.ActualTheme == ElementTheme.Dark;
        IsDark = isDark;
        Color accent = isDark ? theme.DarkColor : theme.LightColor;

        SetBrush("AccentBrush", accent);
        SetBrush("AccentSubtleBrush", Color.FromArgb((byte)(isDark ? 0x33 : 0x1F), accent.R, accent.G, accent.B));
        SetBrush("TitleBarBrush", Color.FromArgb(0x26, accent.R, accent.G, accent.B));
        SetBrush("DividerBrush", isDark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x38, 0x00, 0x00, 0x00));
        SetBrush("SubtleFillBrush", isDark ? Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1F, 0x80, 0x80, 0x80));
        SetBrush("ToastBackgroundBrush", isDark ? Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B) : Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
    }

    private void ApplyHighContrastBrushes()
    {
        IsHighContrast = true;
        IsDark = _root?.ActualTheme == ElementTheme.Dark;

        Color foreground = GetColor(UIColorType.Foreground, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        Color background = GetColor(UIColorType.Background, Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        Color accent = GetColor(UIColorType.Accent, foreground);

        SetBrush("AccentBrush", accent);
        SetBrush("AccentSubtleBrush", Color.FromArgb(0x33, accent.R, accent.G, accent.B));
        SetBrush("TitleBarBrush", Color.FromArgb(0x00, accent.R, accent.G, accent.B));
        SetBrush("DividerBrush", foreground);
        SetBrush("SubtleFillBrush", Color.FromArgb(0x00, background.R, background.G, background.B));
        SetBrush("ToastBackgroundBrush", background);
        SetBrush("DangerBrush", foreground);
    }

    private bool IsHighContrastEnabled()
    {
        try
        {
            return _accessibility?.HighContrast == true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "讀取高對比狀態失敗。");
            return false;
        }
    }

    private Color GetColor(UIColorType type, Color fallback)
    {
        try
        {
            return _uiSettings?.GetColorValue(type) ?? fallback;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "讀取系統色彩失敗（{ColorType}）。", type);
            return fallback;
        }
    }

    private void SetBrush(string key, Color color)
    {
        SolidColorBrush? brush = FindBrush(key);
        if (brush is not null)
        {
            brush.Color = color;
        }
    }

    private SolidColorBrush? FindBrush(string key)
    {
        if (_root is not null
            && _root.Resources.TryGetValue(key, out object? rootValue)
            && rootValue is SolidColorBrush rootBrush)
        {
            return rootBrush;
        }

        if (Application.Current is { } application
            && application.Resources.TryGetValue(key, out object? appValue)
            && appValue is SolidColorBrush appBrush)
        {
            return appBrush;
        }

        return null;
    }
}
