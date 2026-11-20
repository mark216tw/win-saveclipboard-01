using Microsoft.Win32;

namespace Com.VibeCoding.SaveClipboard.Services;

public sealed class StartupService
{
    public const string AutoStartArgument = "--autostart";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SaveClipboard";

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>單次讀取登錄檔，同時取得是否啟用與是否已含 --autostart 參數，減少啟動時 I/O。</summary>
    public (bool IsEnabled, bool HasAutoStartArgument) GetStatus()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
        {
            return (false, false);
        }

        return (true, value.Contains(AutoStartArgument, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 舊版登錄值只含執行檔路徑，無法區分開機啟動與使用者手動啟動。
    /// 缺少參數時回傳 false，由呼叫端補寫成含 <see cref="AutoStartArgument"/> 的格式。
    /// </summary>
    public bool HasAutoStartArgument()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value
            && value.Contains(AutoStartArgument, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            string exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "SaveClipboard.exe");
            key.SetValue(ValueName, $"\"{exePath}\" {AutoStartArgument}", RegistryValueKind.String);
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
