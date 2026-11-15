using Com.VibeCoding.SaveClipboard.Models;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 依來源應用程式提供的剪貼簿標記決定是否保存內容。
/// 密碼管理器等應用程式會設定 <c>ExcludeClipboardContentFromMonitorProcessing</c>
/// 或將 <c>CanIncludeInClipboardHistory</c> 設為 0，本工具必須尊重這些意圖。
/// </summary>
public static class ClipboardPrivacyPolicy
{
    public static ClipboardSkipReason Evaluate(bool hasExcludeMarker, bool? canIncludeInHistory)
    {
        if (hasExcludeMarker)
        {
            return ClipboardSkipReason.ExcludedBySource;
        }

        return canIncludeInHistory == false
            ? ClipboardSkipReason.HistoryDisabledBySource
            : ClipboardSkipReason.None;
    }
}
