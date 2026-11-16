using Com.VibeCoding.SaveClipboard.Models;
using Xunit;

namespace SaveClipboard.Tests;

public class AppSettingsTests
{
    [Fact]
    public void 選取後不關閉預設為關閉()
    {
        Assert.False(new AppSettings().KeepPanelOpenAfterSelect);
    }

    [Fact]
    public void 沒有保存位置時尺寸回到預設值()
    {
        var settings = new AppSettings
        {
            HasPanelBounds = false,
            PanelLeft = 120,
            PanelTop = 80,
            PanelWidth = 900,
            PanelHeight = 700,
        };

        settings.Normalize();

        Assert.Equal(0, settings.PanelLeft);
        Assert.Equal(0, settings.PanelTop);
        Assert.Equal(AppSettings.DefaultPanelWidth, settings.PanelWidth);
        Assert.Equal(AppSettings.DefaultPanelHeight, settings.PanelHeight);
    }

    [Fact]
    public void 有保存位置時保留合法尺寸()
    {
        var settings = new AppSettings
        {
            HasPanelBounds = true,
            PanelLeft = 120,
            PanelTop = 80,
            PanelWidth = 900,
            PanelHeight = 700,
        };

        settings.Normalize();

        Assert.Equal(120, settings.PanelLeft);
        Assert.Equal(80, settings.PanelTop);
        Assert.Equal(900, settings.PanelWidth);
        Assert.Equal(700, settings.PanelHeight);
    }

    [Fact]
    public void 尺寸超出範圍時會被限制()
    {
        var settings = new AppSettings
        {
            HasPanelBounds = true,
            PanelWidth = 10,
            PanelHeight = 99999,
        };

        settings.Normalize();

        Assert.Equal(AppSettings.MinPanelWidth, settings.PanelWidth);
        Assert.Equal(AppSettings.MaxPanelHeight, settings.PanelHeight);
    }

    [Fact]
    public void CopyFrom_會還原所有欄位()
    {
        var source = new AppSettings
        {
            MaxItems = 20,
            AutoStart = true,
            DisplayMode = DisplayMode.Dark,
            ThemeId = "forest-green",
            KeepPanelOpenAfterSelect = true,
            PersistHistory = true,
            HasSeenWelcome = true,
            HasPanelBounds = true,
            PanelLeft = 10,
            PanelTop = 20,
            PanelWidth = 640,
            PanelHeight = 480,
            PanelDpi = 144,
        };
        var target = new AppSettings { ThemeId = "gold" };

        target.CopyFrom(source);

        Assert.Equal(20, target.MaxItems);
        Assert.True(target.AutoStart);
        Assert.Equal(DisplayMode.Dark, target.DisplayMode);
        Assert.Equal("forest-green", target.ThemeId);
        Assert.True(target.KeepPanelOpenAfterSelect);
        Assert.True(target.PersistHistory);
        Assert.True(target.HasSeenWelcome);
        Assert.True(target.HasPanelBounds);
        Assert.Equal(10, target.PanelLeft);
        Assert.Equal(20, target.PanelTop);
        Assert.Equal(640, target.PanelWidth);
        Assert.Equal(480, target.PanelHeight);
        Assert.Equal(144, target.PanelDpi);
    }

    [Fact]
    public void Clone_會複製新欄位()
    {
        var settings = new AppSettings
        {
            KeepPanelOpenAfterSelect = true,
            HasPanelBounds = true,
            PanelLeft = 10,
            PanelTop = 20,
            PanelWidth = 640,
            PanelHeight = 480,
        };

        AppSettings clone = settings.Clone();

        Assert.True(clone.KeepPanelOpenAfterSelect);
        Assert.True(clone.HasPanelBounds);
        Assert.Equal(10, clone.PanelLeft);
        Assert.Equal(20, clone.PanelTop);
        Assert.Equal(640, clone.PanelWidth);
        Assert.Equal(480, clone.PanelHeight);
    }
}
