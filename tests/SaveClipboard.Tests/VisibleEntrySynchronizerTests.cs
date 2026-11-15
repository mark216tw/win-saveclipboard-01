using System.Collections.ObjectModel;
using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class VisibleEntrySynchronizerTests
{
    private static ClipboardEntry Text(string text, bool pinned = false) => new()
    {
        Id = Guid.NewGuid(),
        Kind = ClipboardEntryKind.Text,
        Text = text,
        Key = ClipboardHistoryService.TextKey(text),
        CopiedAt = DateTimeOffset.Now,
        IsPinned = pinned,
    };

    [Fact]
    public void Sync_新項目插入最上方且不重建既有項目()
    {
        ClipboardEntry first = Text("first");
        ClipboardEntry second = Text("second");
        var source = new ObservableCollection<ClipboardEntry> { first };
        var visible = new ObservableCollection<ClipboardEntry> { first };

        source.Insert(0, second);
        VisibleEntrySynchronizer.Sync(visible, source, query: null);

        Assert.Equal(new[] { second, first }, visible);
    }

    [Fact]
    public void Sync_移除來源中不存在的項目()
    {
        ClipboardEntry first = Text("first");
        ClipboardEntry second = Text("second");
        var source = new ObservableCollection<ClipboardEntry> { first, second };
        var visible = new ObservableCollection<ClipboardEntry> { first, second };

        source.Remove(first);
        VisibleEntrySynchronizer.Sync(visible, source, query: null);

        Assert.Equal(new[] { second }, visible);
    }

    [Fact]
    public void Sync_依搜尋字串過濾且保留符合項目()
    {
        ClipboardEntry apple = Text("apple");
        ClipboardEntry banana = Text("banana");
        var source = new ObservableCollection<ClipboardEntry> { apple, banana };
        var visible = new ObservableCollection<ClipboardEntry> { apple, banana };

        VisibleEntrySynchronizer.Sync(visible, source, "app");

        Assert.Equal(new[] { apple }, visible);
    }

    [Fact]
    public void Sync_釘選重排時更新順序()
    {
        ClipboardEntry first = Text("first", pinned: true);
        ClipboardEntry second = Text("second");
        var source = new ObservableCollection<ClipboardEntry> { second, first };
        var visible = new ObservableCollection<ClipboardEntry> { first, second };

        VisibleEntrySynchronizer.Sync(visible, source, query: null);

        Assert.Equal(new[] { second, first }, visible);
    }

    [Fact]
    public void Rebuild_清空後重建符合項目()
    {
        ClipboardEntry apple = Text("apple");
        ClipboardEntry banana = Text("banana");
        var source = new[] { apple, banana };
        var visible = new ObservableCollection<ClipboardEntry> { apple, banana };

        VisibleEntrySynchronizer.Rebuild(visible, source, "ban");

        Assert.Equal(new[] { banana }, visible);
    }
}
