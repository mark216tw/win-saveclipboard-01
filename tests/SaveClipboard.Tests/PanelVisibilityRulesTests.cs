using Com.VibeCoding.SaveClipboard.Helpers;
using Xunit;

namespace SaveClipboard.Tests;

public class PanelVisibilityRulesTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void KeepVisibleDuringPaste_僅置頂且選取即貼上時保持顯示(
        bool alwaysOnTop,
        bool pasteOnSelect,
        bool expected)
    {
        Assert.Equal(expected, PanelVisibilityRules.KeepVisibleDuringPaste(alwaysOnTop, pasteOnSelect));
    }

    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, false)]
    public void ShouldHideAfterCopy_保持顯示時不自動關閉(
        bool autoHideEnabled,
        bool alwaysOnTop,
        bool pasteOnSelect,
        bool expected)
    {
        Assert.Equal(
            expected,
            PanelVisibilityRules.ShouldHideAfterCopy(autoHideEnabled, alwaysOnTop, pasteOnSelect));
    }

    [Fact]
    public void ShouldHideAfterCopy_非自動關閉模式不關閉()
    {
        Assert.False(PanelVisibilityRules.ShouldHideAfterCopy(false, false, true));
    }
}
