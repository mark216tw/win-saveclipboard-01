using Com.VibeCoding.SaveClipboard.Models;
using Xunit;

namespace SaveClipboard.Tests;

public class ThemeCatalogTests
{
    [Fact]
    public void 提供十個主題且識別碼不重複()
    {
        Assert.Equal(10, ThemeCatalog.All.Count);
        Assert.Equal(ThemeCatalog.All.Count, ThemeCatalog.All.Select(theme => theme.Id).Distinct().Count());
    }

    [Fact]
    public void 預設主題存在於清單中()
    {
        Assert.True(ThemeCatalog.Contains(ThemeCatalog.DefaultThemeId));
        Assert.Equal(ThemeCatalog.DefaultThemeId, ThemeCatalog.Default.Id);
    }

    [Fact]
    public void 未知主題回傳預設值()
    {
        ThemeOption theme = ThemeCatalog.Get("not-a-theme");

        Assert.Equal(ThemeCatalog.DefaultThemeId, theme.Id);
    }

    [Fact]
    public void 主題顏色為不透明且明暗不同()
    {
        foreach (ThemeOption theme in ThemeCatalog.All)
        {
            Assert.Equal(0xFF, theme.LightColor.A);
            Assert.Equal(0xFF, theme.DarkColor.A);
            Assert.NotEqual(theme.LightColor, theme.DarkColor);
        }
    }
}
