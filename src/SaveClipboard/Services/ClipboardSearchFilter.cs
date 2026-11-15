using Com.VibeCoding.SaveClipboard.Models;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 歷史清單的搜尋比對。沒有關鍵字時全部符合；有關鍵字時只比對文字項目內容。
/// </summary>
public static class ClipboardSearchFilter
{
    public static bool Matches(ClipboardEntry entry, string? query)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        string keyword = query.Trim();
        return entry.Kind == ClipboardEntryKind.Text
            && entry.Text is not null
            && entry.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
