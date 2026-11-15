using System.Collections.ObjectModel;
using Com.VibeCoding.SaveClipboard.Models;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 讓搜尋結果集合與來源歷史保持同步。只插入、移除或移動有差異的項目，
/// 避免每複製一筆就整批 Clear/Add 造成清單閃爍與捲動位置跳動。
/// </summary>
public static class VisibleEntrySynchronizer
{
    public static void Sync(
        ObservableCollection<ClipboardEntry> visible,
        IReadOnlyList<ClipboardEntry> source,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(source);

        List<ClipboardEntry> desired = source
            .Where(entry => ClipboardSearchFilter.Matches(entry, query))
            .ToList();

        for (int index = visible.Count - 1; index >= 0; index--)
        {
            if (!desired.Contains(visible[index]))
            {
                visible.RemoveAt(index);
            }
        }

        for (int target = 0; target < desired.Count; target++)
        {
            ClipboardEntry entry = desired[target];
            int current = visible.IndexOf(entry);
            if (current < 0)
            {
                visible.Insert(target, entry);
            }
            else if (current != target)
            {
                visible.Move(current, target);
            }
        }
    }

    public static void Rebuild(
        ObservableCollection<ClipboardEntry> visible,
        IReadOnlyList<ClipboardEntry> source,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(source);

        visible.Clear();
        foreach (ClipboardEntry entry in source)
        {
            if (ClipboardSearchFilter.Matches(entry, query))
            {
                visible.Add(entry);
            }
        }
    }
}
