using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 可選的歷史持久化。整份歷史以 Windows DPAPI（目前使用者）加密後寫入
/// %LOCALAPPDATA%\SaveClipboard\history.dat，預設關閉。
/// </summary>
public sealed class HistoryPersistenceService
{
    private readonly string _path;
    private readonly ILogger<HistoryPersistenceService> _logger;

    public HistoryPersistenceService(ILogger<HistoryPersistenceService> logger)
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SaveClipboard",
                "history.dat"),
            logger)
    {
    }

    public HistoryPersistenceService(string path, ILogger<HistoryPersistenceService> logger)
    {
        _path = path;
        _logger = logger;
    }

    public IReadOnlyList<ClipboardEntry> Load()
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<ClipboardEntry>();
        }

        try
        {
            byte[] encrypted = File.ReadAllBytes(_path);
            byte[] json = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            PersistedHistoryFile? file = JsonSerializer.Deserialize(json, HistoryPersistenceJsonContext.Default.PersistedHistoryFile);
            if (file?.Entries is null)
            {
                return Array.Empty<ClipboardEntry>();
            }

            var entries = new List<ClipboardEntry>(file.Entries.Count);
            foreach (PersistedHistoryEntry item in file.Entries)
            {
                ClipboardEntry? entry = ToEntry(item);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or JsonException
            or FormatException)
        {
            _logger.LogWarning(ex, "讀取加密歷史失敗，將以空白歷史啟動。");
            return Array.Empty<ClipboardEntry>();
        }
    }

    public bool TrySave(IReadOnlyList<ClipboardEntry> entries)
    {
        string? directory = Path.GetDirectoryName(_path);
        string tempPath = _path + ".tmp";
        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new PersistedHistoryFile
            {
                Entries = entries.Select(ToPersisted).ToList(),
            };

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(file, HistoryPersistenceJsonContext.Default.PersistedHistoryFile);
            byte[] encrypted = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or JsonException)
        {
            _logger.LogWarning(ex, "寫入加密歷史失敗。");
            TryDelete(tempPath);
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "刪除加密歷史失敗。");
        }
    }

    private static PersistedHistoryEntry ToPersisted(ClipboardEntry entry) => new()
    {
        Id = entry.Id,
        Kind = entry.Kind,
        Text = entry.Text,
        ImageBase64 = entry.ImageBytes,
        ThumbnailBase64 = entry.ThumbnailBytes,
        PixelHashBase64 = entry.ImagePixelHash,
        Key = entry.Key,
        CopiedAt = entry.CopiedAt,
        IsPinned = entry.IsPinned,
    };

    private static ClipboardEntry? ToEntry(PersistedHistoryEntry item)
    {
        if (item.Kind == ClipboardEntryKind.Text && string.IsNullOrEmpty(item.Text))
        {
            return null;
        }

        if (item.Kind == ClipboardEntryKind.Image && item.ImageBase64 is not { Length: > 0 })
        {
            return null;
        }

        return new ClipboardEntry
        {
            Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
            Kind = item.Kind,
            Text = item.Text,
            ImageBytes = NonEmpty(item.ImageBase64),
            ThumbnailBytes = NonEmpty(item.ThumbnailBase64),
            ImagePixelHash = NonEmpty(item.PixelHashBase64),
            Key = item.Key,
            CopiedAt = item.CopiedAt,
            IsPinned = item.IsPinned,
        };
    }

    private static byte[]? NonEmpty(byte[]? data) =>
        data is { Length: > 0 } ? data : null;

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

internal sealed class PersistedHistoryFile
{
    public int Version { get; set; } = 1;

    public List<PersistedHistoryEntry> Entries { get; set; } = [];
}

internal sealed class PersistedHistoryEntry
{
    public Guid Id { get; set; }

    public ClipboardEntryKind Kind { get; set; }

    public string? Text { get; set; }

    public byte[]? ImageBase64 { get; set; }

    public byte[]? ThumbnailBase64 { get; set; }

    public byte[]? PixelHashBase64 { get; set; }

    public string Key { get; set; } = string.Empty;

    public DateTimeOffset CopiedAt { get; set; }

    public bool IsPinned { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PersistedHistoryFile))]
internal sealed partial class HistoryPersistenceJsonContext : JsonSerializerContext
{
}
