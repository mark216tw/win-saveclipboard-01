namespace Com.VibeCoding.SaveClipboard.Models;

/// <summary>
/// 剪貼簿內容被略過的原因，用於診斷記錄（不記錄內容本身）。
/// </summary>
public enum ClipboardSkipReason
{
    None = 0,
    ExcludedBySource = 1,
    HistoryDisabledBySource = 2,
}
