using System.Globalization;
using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.UI.Xaml;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 供 ListView DataTemplate 的編譯式 x:Bind 函式繫結使用，
/// 避免 IValueConverter 的裝箱配置與反射查詢。
/// </summary>
public static class EntryDisplayHelper
{
    public static Visibility ImageVisibility(ClipboardEntryKind kind) =>
        kind == ClipboardEntryKind.Image ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility TextVisibility(ClipboardEntryKind kind) =>
        kind == ClipboardEntryKind.Text ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility BoolVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static string FormatCopiedAt(DateTimeOffset copiedAt) =>
        copiedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
