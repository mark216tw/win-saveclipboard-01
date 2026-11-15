namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 面板顯示決策：面板置頂且啟用選取即貼上時，貼上期間保持面板顯示在最上層，
/// 其餘情況沿用自動關閉行為。
/// </summary>
public static class PanelVisibilityRules
{
    public static bool KeepVisibleDuringPaste(bool alwaysOnTop, bool pasteOnSelect) =>
        alwaysOnTop && pasteOnSelect;

    public static bool ShouldHideAfterCopy(bool autoHideEnabled, bool alwaysOnTop, bool pasteOnSelect) =>
        autoHideEnabled && !KeepVisibleDuringPaste(alwaysOnTop, pasteOnSelect);
}
