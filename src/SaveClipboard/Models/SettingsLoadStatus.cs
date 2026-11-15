namespace Com.VibeCoding.SaveClipboard.Models;

/// <summary>
/// 設定檔載入結果。損毀時會備份原檔並回到預設值，讓使用者有機會自行取回。
/// </summary>
public enum SettingsLoadStatus
{
    Ok = 0,
    Missing = 1,
    Recovered = 2,
    Unavailable = 3,
}
