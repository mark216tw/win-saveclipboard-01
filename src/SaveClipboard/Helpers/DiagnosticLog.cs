namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// DI 尚未建立或原生訊息回呼發生例外時使用的最後防線。
/// 寫入 %LOCALAPPDATA%\SaveClipboard\Logs\crash.log，超過上限時重建避免無限成長。
/// </summary>
internal static class DiagnosticLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveClipboard",
        "Logs");

    public static string CrashLogPath { get; } = Path.Combine(LogDirectory, "crash.log");

    public static void Write(string source, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            lock (Gate)
            {
                var info = new FileInfo(CrashLogPath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.WriteAllText(CrashLogPath, string.Empty);
                }

                File.AppendAllText(
                    CrashLogPath,
                    $"[{DateTimeOffset.Now:O}] {source}: {exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 日誌本身失敗時不能再拋例外，否則會遮蔽原始錯誤
        }
    }
}
