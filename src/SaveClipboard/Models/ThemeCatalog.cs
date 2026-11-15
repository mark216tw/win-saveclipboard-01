using Windows.UI;

namespace Com.VibeCoding.SaveClipboard.Models;

public static class ThemeCatalog
{
    public const string DefaultThemeId = "classic-blue";

    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new("classic-blue", "經典藍", Color.FromArgb(0xFF, 0x2F, 0x4B, 0x7C), Color.FromArgb(0xFF, 0x7C, 0x9B, 0xE0)),
        new("forest-green", "森林綠", Color.FromArgb(0xFF, 0x2E, 0x6B, 0x3A), Color.FromArgb(0xFF, 0x74, 0xC4, 0x86)),
        new("dusk-purple", "暮色紫", Color.FromArgb(0xFF, 0x5B, 0x3E, 0x8E), Color.FromArgb(0xFF, 0xB0, 0x9A, 0xE8)),
        new("sunset-orange", "暖陽橙", Color.FromArgb(0xFF, 0xB4, 0x53, 0x09), Color.FromArgb(0xFF, 0xF2, 0xA7, 0x5A)),
        new("rose-red", "玫瑰紅", Color.FromArgb(0xFF, 0xA3, 0x2E, 0x50), Color.FromArgb(0xFF, 0xEE, 0x8A, 0xA8)),
        new("teal", "湖水青", Color.FromArgb(0xFF, 0x0E, 0x6B, 0x73), Color.FromArgb(0xFF, 0x58, 0xC7, 0xCE)),
        new("gold", "金黃", Color.FromArgb(0xFF, 0x8A, 0x6D, 0x00), Color.FromArgb(0xFF, 0xE3, 0xC2, 0x4B)),
        new("graphite", "石墨灰", Color.FromArgb(0xFF, 0x4B, 0x55, 0x63), Color.FromArgb(0xFF, 0xAE, 0xB7, 0xC2)),
        new("indigo", "靛青", Color.FromArgb(0xFF, 0x3F, 0x4F, 0xB5), Color.FromArgb(0xFF, 0x94, 0xA1, 0xF5)),
        new("coral", "珊瑚紅", Color.FromArgb(0xFF, 0xB0, 0x46, 0x3A), Color.FromArgb(0xFF, 0xF4, 0x98, 0x8A)),
    ];

    public static ThemeOption Default => All[0];

    public static bool Contains(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && All.Any(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));

    public static ThemeOption Get(string? id) =>
        All.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
