using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardHistoryServiceTests
{
    private static ClipboardHistoryService CreateService(int maxItems = 10) =>
        new(maxItems, () => new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void AddText_新增項目到最上方()
    {
        var service = CreateService();
        service.AddText("first");
        service.AddText("second");

        Assert.Equal(2, service.Items.Count);
        Assert.Equal("second", service.Items[0].Text);
        Assert.Equal("first", service.Items[1].Text);
    }

    [Fact]
    public void AddText_超過上限時淘汰最舊的未釘選項目()
    {
        var service = CreateService(maxItems: AppSettings.MinMaxItems);
        for (int i = 0; i <= AppSettings.MinMaxItems; i++)
        {
            service.AddText($"item-{i}");
        }

        Assert.Equal(AppSettings.MinMaxItems, service.Items.Count);
        Assert.DoesNotContain(service.Items, item => item.Text == "item-0");
        Assert.Equal($"item-{AppSettings.MinMaxItems}", service.Items[0].Text);
    }

    [Fact]
    public void AddText_釘選項目不會被淘汰()
    {
        var service = CreateService(maxItems: AppSettings.MinMaxItems);
        service.AddText("pinned");
        service.TogglePin(service.Items[0].Id);

        for (int i = 0; i <= AppSettings.MinMaxItems; i++)
        {
            service.AddText($"item-{i}");
        }

        Assert.Contains(service.Items, item => item.Text == "pinned");
        Assert.Equal("pinned", service.Items[0].Text);
        Assert.Equal($"item-{AppSettings.MinMaxItems}", service.Items[1].Text);
        Assert.DoesNotContain(service.Items, item => item.Text == "item-0");
    }

    [Fact]
    public void 全部釘選時可超過上限()
    {
        var service = CreateService(maxItems: AppSettings.MinMaxItems);
        for (int i = 0; i <= AppSettings.MinMaxItems; i++)
        {
            ClipboardEntry entry = service.AddText($"pinned-{i}");
            service.TogglePin(entry.Id);
        }

        Assert.Equal(AppSettings.MinMaxItems + 1, service.Items.Count);
    }

    [Fact]
    public void AddText_重複內容會移到最前面且不重複()
    {
        var service = CreateService();
        service.AddText("alpha");
        service.AddText("beta");
        service.AddText("alpha");

        Assert.Equal(2, service.Items.Count);
        Assert.Equal("alpha", service.Items[0].Text);
        Assert.Equal("beta", service.Items[1].Text);
    }

    [Fact]
    public void AddText_重複內容保留釘選狀態()
    {
        var service = CreateService();
        service.AddText("alpha");
        var id = service.Items[0].Id;
        service.TogglePin(id);

        service.AddText("alpha");

        Assert.Single(service.Items);
        Assert.True(service.Items[0].IsPinned);
    }

    [Fact]
    public void MaxItems_調低時會立即淘汰多餘項目()
    {
        var service = CreateService(maxItems: AppSettings.MinMaxItems + 2);
        for (int i = 0; i <= AppSettings.MinMaxItems + 1; i++)
        {
            service.AddText($"item-{i}");
        }

        service.MaxItems = AppSettings.MinMaxItems;

        Assert.Equal(AppSettings.MinMaxItems, service.Items.Count);
        Assert.Contains(service.Items, item => item.Text == $"item-{AppSettings.MinMaxItems + 1}");
        Assert.Contains(service.Items, item => item.Text == "item-2");
        Assert.DoesNotContain(service.Items, item => item.Text == "item-0");
        Assert.DoesNotContain(service.Items, item => item.Text == "item-1");
    }

    [Fact]
    public void TogglePin_不存在的項目回傳false()
    {
        var service = CreateService();
        Assert.False(service.TogglePin(Guid.NewGuid()));
    }

    [Fact]
    public void SetPinned_可指定釘選狀態()
    {
        var service = CreateService();
        service.AddText("a");
        Guid id = service.Items[0].Id;

        Assert.True(service.SetPinned(id, true));
        Assert.True(service.Items[0].IsPinned);

        Assert.True(service.SetPinned(id, false));
        Assert.False(service.Items[0].IsPinned);

        Assert.False(service.SetPinned(Guid.NewGuid(), true));
    }

    [Fact]
    public void SetPinned_釘選項目移到最上方()
    {
        var service = CreateService();
        service.AddText("a");
        service.AddText("b");
        service.AddText("c");
        Guid oldest = service.Items[2].Id;

        Assert.True(service.SetPinned(oldest, true));

        Assert.Equal("a", service.Items[0].Text);
        Assert.Equal("c", service.Items[1].Text);
        Assert.Equal("b", service.Items[2].Text);
    }

    [Fact]
    public void SetPinned_取消釘選後留在未釘選區上方()
    {
        var service = CreateService();
        service.AddText("a");
        service.AddText("b");
        Guid pinnedId = service.Items[1].Id;
        service.SetPinned(pinnedId, true);
        service.AddText("c");

        Assert.Equal("a", service.Items[0].Text);

        Assert.True(service.SetPinned(pinnedId, false));

        Assert.False(service.Items[0].IsPinned);
        Assert.Equal("a", service.Items[0].Text);
    }

    [Fact]
    public void AddText_新項目排在釘選項目之後()
    {
        var service = CreateService();
        service.AddText("pinned");
        service.SetPinned(service.Items[0].Id, true);

        service.AddText("newest");

        Assert.Equal("pinned", service.Items[0].Text);
        Assert.True(service.Items[0].IsPinned);
        Assert.Equal("newest", service.Items[1].Text);
    }

    [Fact]
    public void AddText_重複內容保留釘選狀態並留在釘選區()
    {
        var service = CreateService();
        service.AddText("alpha");
        service.AddText("beta");
        service.SetPinned(service.Items[1].Id, true);

        service.AddText("alpha");

        Assert.Equal("alpha", service.Items[0].Text);
        Assert.True(service.Items[0].IsPinned);
        Assert.Equal("beta", service.Items[1].Text);
    }

    [Fact]
    public void Remove_刪除指定項目()
    {
        var service = CreateService();
        service.AddText("a");
        Guid id = service.Items[0].Id;

        Assert.True(service.Remove(id));
        Assert.Empty(service.Items);
        Assert.False(service.Remove(id));
    }

    [Fact]
    public void ClearAll_清空含釘選項目()
    {
        var service = CreateService();
        service.AddText("a");
        service.TogglePin(service.Items[0].Id);
        service.AddImage(new byte[] { 1, 2, 3 }, new byte[] { 4, 5 });

        service.ClearAll();

        Assert.Empty(service.Items);
    }

    [Fact]
    public void AddImage_相同圖片不重複()
    {
        var service = CreateService();
        byte[] png = [1, 2, 3, 4];
        byte[] thumb = [9];
        service.AddImage(png, thumb);
        service.AddImage(png, thumb);

        Assert.Single(service.Items);
    }

    [Fact]
    public void MaxItems_限制在範圍內()
    {
        var service = CreateService(maxItems: 5);
        service.MaxItems = 9999;
        Assert.Equal(AppSettings.MaxMaxItems, service.MaxItems);
        service.MaxItems = 0;
        Assert.Equal(AppSettings.MinMaxItems, service.MaxItems);
    }

    [Fact]
    public void AddImage_超過圖片位元組上限時淘汰最舊未釘選圖片()
    {
        var service = new ClipboardHistoryService(
            10,
            () => new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero),
            maxTotalImageBytes: 8);

        service.AddImage([1, 2, 3, 4, 5], [1]);
        service.AddImage([6, 7, 8, 9, 10], [1]);

        Assert.Single(service.Items);
        Assert.Equal(6, service.Items[0].ImageBytes![0]);
    }

    [Fact]
    public void AddImage_釘選圖片不受圖片位元組上限限制()
    {
        var service = new ClipboardHistoryService(
            10,
            () => new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero),
            maxTotalImageBytes: 6);

        ClipboardEntry pinned = service.AddImage([1, 2, 3, 4, 5], [1]);
        service.SetPinned(pinned.Id, true);
        service.AddImage([6, 7, 8], [1]);

        Assert.Single(service.Items);
        Assert.True(service.Items[0].IsPinned);
        Assert.Equal(5, service.Items[0].ImageBytes!.Length);
    }

    [Fact]
    public void AddImage_可使用預先計算的雜湊()
    {
        var service = CreateService();
        byte[] png = [1, 2, 3];
        byte[] hash = ClipboardHistoryService.ComputePixelHash(png);

        service.AddImage(png, [9], hash);

        Assert.Equal(hash, service.Items[0].ImagePixelHash);
    }

    [Fact]
    public void MaxTotalImageBytes_限制在有效範圍內()
    {
        var service = CreateService();
        service.MaxTotalImageBytes = 0;
        Assert.Equal(1, service.MaxTotalImageBytes);
    }

    [Fact]
    public void Restore_把刪除項目插回原索引()
    {
        var service = CreateService();
        ClipboardEntry first = service.AddText("first");
        service.AddText("second");
        service.AddText("third");

        Assert.True(service.Remove(first.Id));
        Assert.True(service.Restore(first, 0));

        Assert.Equal("first", service.Items[0].Text);
        Assert.Equal(3, service.Items.Count);
    }

    [Fact]
    public void Restore_相同鍵不重複插入()
    {
        var service = CreateService();
        ClipboardEntry entry = service.AddText("same");

        Assert.False(service.Restore(entry, 0));
        Assert.Single(service.Items);
    }

    [Fact]
    public void RestoreMissing_保留啟動期間新項目並補上未重複的持久化項目()
    {
        var service = CreateService();
        service.AddText("captured-during-startup");

        var persisted = new[]
        {
            new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Kind = ClipboardEntryKind.Text,
                Text = "captured-during-startup",
                Key = ClipboardHistoryService.TextKey("captured-during-startup"),
                CopiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            },
            new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Kind = ClipboardEntryKind.Text,
                Text = "persisted-pinned",
                Key = ClipboardHistoryService.TextKey("persisted-pinned"),
                CopiedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                IsPinned = true,
            },
            new ClipboardEntry
            {
                Id = Guid.NewGuid(),
                Kind = ClipboardEntryKind.Text,
                Text = "persisted-older",
                Key = ClipboardHistoryService.TextKey("persisted-older"),
                CopiedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
            },
        };

        service.RestoreMissing(persisted);

        Assert.Equal(3, service.Items.Count);
        Assert.Equal("persisted-pinned", service.Items[0].Text);
        Assert.Equal("captured-during-startup", service.Items[1].Text);
        Assert.Equal("persisted-older", service.Items[2].Text);
    }
}
