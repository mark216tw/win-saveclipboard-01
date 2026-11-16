namespace Com.VibeCoding.SaveClipboard.Models;

public sealed class AppSettings
{
    public const int DefaultMaxItems = 30;
    public const int MinMaxItems = 10;
    public const int MaxMaxItems = 50;

    // 面板尺寸以實體像素保存，載入時只做合理範圍檢查；實際可見範圍由視窗依螢幕工作區驗證
    public const int DefaultPanelWidth = 400;
    public const int DefaultPanelHeight = 620;
    public const int MinPanelWidth = 200;
    public const int MinPanelHeight = 300;
    public const int MaxPanelWidth = 2400;
    public const int MaxPanelHeight = 2400;
    public const int DefaultPanelDpi = 96;
    public const int MinPanelDpi = 48;
    public const int MaxPanelDpi = 384;

    public int MaxItems { get; set; } = DefaultMaxItems;

    public bool AutoStart { get; set; }

    public DisplayMode DisplayMode { get; set; } = DisplayMode.System;

    public string ThemeId { get; set; } = ThemeCatalog.DefaultThemeId;

    /// <summary>選取項目後保持面板顯示，不自動關閉。</summary>
    public bool KeepPanelOpenAfterSelect { get; set; }

    /// <summary>是否以 DPAPI 加密保存歷史到本機；預設只存在記憶體。</summary>
    public bool PersistHistory { get; set; }

    /// <summary>是否已顯示首次啟動導引。</summary>
    public bool HasSeenWelcome { get; set; }

    /// <summary>是否已保存面板位置與大小。</summary>
    public bool HasPanelBounds { get; set; }

    public int PanelLeft { get; set; }

    public int PanelTop { get; set; }

    public int PanelWidth { get; set; } = DefaultPanelWidth;

    public int PanelHeight { get; set; } = DefaultPanelHeight;

    /// <summary>保存面板尺寸時所在螢幕的 DPI，用於跨 DPI 還原時換算。</summary>
    public int PanelDpi { get; set; } = DefaultPanelDpi;

    public AppSettings Clone() => new()
    {
        MaxItems = MaxItems,
        AutoStart = AutoStart,
        DisplayMode = DisplayMode,
        ThemeId = ThemeId,
        KeepPanelOpenAfterSelect = KeepPanelOpenAfterSelect,
        PersistHistory = PersistHistory,
        HasSeenWelcome = HasSeenWelcome,
        HasPanelBounds = HasPanelBounds,
        PanelLeft = PanelLeft,
        PanelTop = PanelTop,
        PanelWidth = PanelWidth,
        PanelHeight = PanelHeight,
        PanelDpi = PanelDpi,
    };

    public void CopyFrom(AppSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        MaxItems = other.MaxItems;
        AutoStart = other.AutoStart;
        DisplayMode = other.DisplayMode;
        ThemeId = other.ThemeId;
        KeepPanelOpenAfterSelect = other.KeepPanelOpenAfterSelect;
        PersistHistory = other.PersistHistory;
        HasSeenWelcome = other.HasSeenWelcome;
        HasPanelBounds = other.HasPanelBounds;
        PanelLeft = other.PanelLeft;
        PanelTop = other.PanelTop;
        PanelWidth = other.PanelWidth;
        PanelHeight = other.PanelHeight;
        PanelDpi = other.PanelDpi;
    }

    public void Normalize()
    {
        MaxItems = Math.Clamp(MaxItems, MinMaxItems, MaxMaxItems);

        if (!Enum.IsDefined(DisplayMode))
        {
            DisplayMode = DisplayMode.System;
        }

        if (!ThemeCatalog.Contains(ThemeId))
        {
            ThemeId = ThemeCatalog.DefaultThemeId;
        }

        PanelWidth = Math.Clamp(PanelWidth, MinPanelWidth, MaxPanelWidth);
        PanelHeight = Math.Clamp(PanelHeight, MinPanelHeight, MaxPanelHeight);
        PanelDpi = Math.Clamp(PanelDpi, MinPanelDpi, MaxPanelDpi);

        if (!HasPanelBounds)
        {
            PanelLeft = 0;
            PanelTop = 0;
            PanelWidth = DefaultPanelWidth;
            PanelHeight = DefaultPanelHeight;
            PanelDpi = DefaultPanelDpi;
        }
    }
}
