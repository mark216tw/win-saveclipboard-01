using System.Text.Json;
using System.Text.Json.Serialization;
using Com.VibeCoding.SaveClipboard.Models;

namespace Com.VibeCoding.SaveClipboard.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SaveClipboard", "settings.json"))
    {
    }

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    /// <summary>最近一次 <see cref="Load"/> 的結果。</summary>
    public SettingsLoadStatus LastLoadStatus { get; private set; } = SettingsLoadStatus.Ok;

    /// <summary>設定檔損毀時保留原檔的路徑，沒有備份時為 <c>null</c>。</summary>
    public string? LastBackupPath { get; private set; }

    public AppSettings Load()
    {
        LastBackupPath = null;

        if (!File.Exists(_settingsPath))
        {
            LastLoadStatus = SettingsLoadStatus.Missing;
            return new AppSettings();
        }

        string json;
        try
        {
            json = File.ReadAllText(_settingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastLoadStatus = SettingsLoadStatus.Unavailable;
            return new AppSettings();
        }

        try
        {
            AppSettings? settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            if (settings is null)
            {
                return RecoverFromCorruptFile();
            }

            settings.Normalize();
            LastLoadStatus = SettingsLoadStatus.Ok;
            return settings;
        }
        catch (JsonException)
        {
            return RecoverFromCorruptFile();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
        string tempPath = _settingsPath + ".tmp";
        try
        {
            // 先寫暫存檔再置換，避免寫入中斷造成設定檔毀損
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private AppSettings RecoverFromCorruptFile()
    {
        LastLoadStatus = SettingsLoadStatus.Recovered;
        LastBackupPath = BackupCorruptFile();
        return new AppSettings();
    }

    private string? BackupCorruptFile()
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        // 保留原檔讓使用者能自行取回或回報，不以時間覆蓋既有備份
        string backupPath = Path.Combine(directory, $"settings.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");

        try
        {
            File.Move(_settingsPath, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Copy(_settingsPath, backupPath, overwrite: true);
                return backupPath;
            }
            catch (Exception copyException) when (copyException is IOException or UnauthorizedAccessException)
            {
                // 無法備份時仍以預設值啟動，不讓載入流程失敗
                return null;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 清不掉暫存檔不影響主要錯誤
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
