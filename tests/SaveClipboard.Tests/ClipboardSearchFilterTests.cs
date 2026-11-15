using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardSearchFilterTests
{
    private static ClipboardEntry CreateText(string text) => new()
    {
        Id = Guid.NewGuid(),
        Kind = ClipboardEntryKind.Text,
        Text = text,
        Key = Guid.NewGuid().ToString("N"),
        CopiedAt = DateTimeOffset.Now,
    };

    [Fact]
    public void 沒有關鍵字時全部符合()
    {
        ClipboardEntry entry = CreateText("hello");

        Assert.True(ClipboardSearchFilter.Matches(entry, null));
        Assert.True(ClipboardSearchFilter.Matches(entry, string.Empty));
        Assert.True(ClipboardSearchFilter.Matches(entry, "   "));
    }

    [Fact]
    public void 文字包含關鍵字時符合()
    {
        ClipboardEntry entry = CreateText("SaveClipboard 測試內容");

        Assert.True(ClipboardSearchFilter.Matches(entry, "測試"));
    }

    [Fact]
    public void 關鍵字比對忽略大小寫()
    {
        ClipboardEntry entry = CreateText("Hello World");

        Assert.True(ClipboardSearchFilter.Matches(entry, "hello"));
        Assert.True(ClipboardSearchFilter.Matches(entry, "WORLD"));
    }

    [Fact]
    public void 關鍵字前後空白會被忽略()
    {
        ClipboardEntry entry = CreateText("alpha beta");

        Assert.True(ClipboardSearchFilter.Matches(entry, "  beta  "));
    }

    [Fact]
    public void 文字不含關鍵字時不符合()
    {
        ClipboardEntry entry = CreateText("alpha");

        Assert.False(ClipboardSearchFilter.Matches(entry, "beta"));
    }

    [Fact]
    public void 圖片項目不會符合任何關鍵字()
    {
        var entry = new ClipboardEntry
        {
            Id = Guid.NewGuid(),
            Kind = ClipboardEntryKind.Image,
            ImageBytes = [1, 2, 3],
            Key = Guid.NewGuid().ToString("N"),
            CopiedAt = DateTimeOffset.Now,
        };

        Assert.False(ClipboardSearchFilter.Matches(entry, "圖片"));
        Assert.True(ClipboardSearchFilter.Matches(entry, string.Empty));
    }

    [Fact]
    public void 文字為空時不會符合關鍵字()
    {
        var entry = new ClipboardEntry
        {
            Id = Guid.NewGuid(),
            Kind = ClipboardEntryKind.Text,
            Key = Guid.NewGuid().ToString("N"),
            CopiedAt = DateTimeOffset.Now,
        };

        Assert.False(ClipboardSearchFilter.Matches(entry, "abc"));
    }
}
