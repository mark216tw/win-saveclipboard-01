using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Com.VibeCoding.SaveClipboard.Models;

namespace Com.VibeCoding.SaveClipboard.Services;

public sealed class ClipboardHistoryService
{
    public const long DefaultMaxTotalImageBytes = 100L * 1024 * 1024;

    private readonly Func<DateTimeOffset> _now;
    private int _maxItems;
    private long _maxTotalImageBytes;

    public ClipboardHistoryService(
        int maxItems,
        Func<DateTimeOffset>? now = null,
        long maxTotalImageBytes = DefaultMaxTotalImageBytes)
    {
        _maxItems = Math.Clamp(maxItems, AppSettings.MinMaxItems, AppSettings.MaxMaxItems);
        _maxTotalImageBytes = Math.Max(1, maxTotalImageBytes);
        _now = now ?? (static () => DateTimeOffset.Now);
        Items = new ObservableCollection<ClipboardEntry>();
    }

    public ObservableCollection<ClipboardEntry> Items { get; }

    public int MaxItems
    {
        get => _maxItems;
        set
        {
            int clamped = Math.Clamp(value, AppSettings.MinMaxItems, AppSettings.MaxMaxItems);
            if (clamped == _maxItems)
            {
                return;
            }

            _maxItems = clamped;
            Trim();
        }
    }

    public long MaxTotalImageBytes
    {
        get => _maxTotalImageBytes;
        set
        {
            long clamped = Math.Max(1, value);
            if (clamped == _maxTotalImageBytes)
            {
                return;
            }

            _maxTotalImageBytes = clamped;
            Trim();
        }
    }

    public static string TextKey(string text) =>
        "T:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string ImageKey(byte[] pixelHash) => "I:" + Convert.ToHexString(pixelHash);

    public static byte[] ComputePixelHash(ReadOnlySpan<byte> bytes) => SHA256.HashData(bytes);

    public ClipboardEntry AddText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var entry = new ClipboardEntry
        {
            Id = Guid.NewGuid(),
            Kind = ClipboardEntryKind.Text,
            Text = text,
            Key = TextKey(text),
            CopiedAt = _now(),
        };
        return Add(entry);
    }

    public ClipboardEntry AddImage(byte[] pngBytes, byte[] thumbnailBytes) =>
        AddImage(pngBytes, thumbnailBytes, ComputePixelHash(pngBytes));

    public ClipboardEntry AddImage(byte[] pngBytes, byte[] thumbnailBytes, byte[] pixelHash)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentNullException.ThrowIfNull(thumbnailBytes);
        ArgumentNullException.ThrowIfNull(pixelHash);

        var entry = new ClipboardEntry
        {
            Id = Guid.NewGuid(),
            Kind = ClipboardEntryKind.Image,
            ImageBytes = pngBytes,
            ThumbnailBytes = thumbnailBytes,
            ImagePixelHash = pixelHash,
            Key = ImageKey(pixelHash),
            CopiedAt = _now(),
        };
        return Add(entry);
    }

    public ClipboardEntry Add(ClipboardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ClipboardEntry? existing = Items.FirstOrDefault(item => item.Key == entry.Key);
        if (existing is not null)
        {
            Items.Remove(existing);
            entry.CopiedAt = _now();
            entry.IsPinned = existing.IsPinned;
            Items.Insert(0, entry);
            RepositionPinned();
            return entry;
        }

        Items.Insert(0, entry);
        RepositionPinned();
        Trim();
        return entry;
    }

    public bool TogglePin(Guid id)
    {
        ClipboardEntry? entry = Items.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        entry.IsPinned = !entry.IsPinned;
        RepositionPinned();
        Trim();
        return true;
    }

    public bool SetPinned(Guid id, bool isPinned)
    {
        ClipboardEntry? entry = Items.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        entry.IsPinned = isPinned;
        RepositionPinned();
        Trim();
        return true;
    }

    public bool Remove(Guid id)
    {
        ClipboardEntry? entry = Items.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return false;
        }

        return Items.Remove(entry);
    }

    public void ClearAll() => Items.Clear();

    /// <summary>載入已保存的歷史；保留原本順序並套用目前上限。</summary>
    public void Restore(IEnumerable<ClipboardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Items.Clear();
        foreach (ClipboardEntry entry in entries)
        {
            Items.Add(entry);
        }

        Trim();
    }

    /// <summary>把刪除的項目插回原本位置；相同內容已存在時略過。</summary>
    public bool Restore(ClipboardEntry entry, int index)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (Items.Any(item => item.Key == entry.Key))
        {
            return false;
        }

        Items.Insert(Math.Clamp(index, 0, Items.Count), entry);
        Trim();
        return true;
    }

    private void RepositionPinned()
    {
        if (!Items.Any(static item => item.IsPinned))
        {
            return;
        }

        // 釘選項目固定排在清單上方，區內維持原本的新舊順序
        List<ClipboardEntry> desired = Items
            .Where(static item => item.IsPinned)
            .Concat(Items.Where(static item => !item.IsPinned))
            .ToList();

        for (int target = 0; target < desired.Count; target++)
        {
            int current = Items.IndexOf(desired[target]);
            if (current != target)
            {
                Items.Move(current, target);
            }
        }
    }

    public void Trim()
    {
        // 上限只計算未釘選項目；釘選項目不受上限限制
        int unpinned = Items.Count(static item => !item.IsPinned);
        while (unpinned > _maxItems)
        {
            ClipboardEntry? oldestUnpinned = Items.LastOrDefault(static item => !item.IsPinned);
            if (oldestUnpinned is null)
            {
                return;
            }

            Items.Remove(oldestUnpinned);
            unpinned--;
        }

        // 圖片總位元組上限同樣只淘汰未釘選項目，避免大量大圖長期佔用記憶體
        long imageBytes = Items.Sum(static item => ItemImageBytes(item));
        while (imageBytes > _maxTotalImageBytes)
        {
            ClipboardEntry? oldestImage = Items.LastOrDefault(
                static item => !item.IsPinned && item.ImageBytes is { Length: > 0 });
            if (oldestImage is null)
            {
                return;
            }

            imageBytes -= ItemImageBytes(oldestImage);
            Items.Remove(oldestImage);
        }
    }

    private static long ItemImageBytes(ClipboardEntry item) =>
        (long)(item.ImageBytes?.Length ?? 0) + (item.ThumbnailBytes?.Length ?? 0);
}
