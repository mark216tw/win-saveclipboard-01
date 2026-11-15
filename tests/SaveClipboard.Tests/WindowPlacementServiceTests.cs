using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Services;
using Windows.Graphics;
using Xunit;

namespace SaveClipboard.Tests;

public class WindowPlacementServiceTests
{
    [Fact]
    public void ScaleForDpi_依DPI換算尺寸()
    {
        RectInt32 scaled = WindowPlacementService.ScaleForDpi(new RectInt32(10, 20, 400, 600), 96, 192);

        Assert.Equal(10, scaled.X);
        Assert.Equal(20, scaled.Y);
        Assert.Equal(800, scaled.Width);
        Assert.Equal(1200, scaled.Height);
    }

    [Fact]
    public void ClampToWorkArea_限制在可見工作區()
    {
        var workArea = new NativeMethods.RECT
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080,
        };

        RectInt32 clamped = WindowPlacementService.ClampToWorkArea(
            new RectInt32(-100, -50, 5000, 4000),
            workArea);

        Assert.True(clamped.X >= workArea.Left);
        Assert.True(clamped.Y >= workArea.Top);
        Assert.True(clamped.X + clamped.Width <= workArea.Right);
        Assert.True(clamped.Y + clamped.Height <= workArea.Bottom);
    }

    [Fact]
    public void ScaleForDpi_無效DPI時退回1倍()
    {
        RectInt32 scaled = WindowPlacementService.ScaleForDpi(new RectInt32(0, 0, 400, 600), 0, 0);

        Assert.Equal(400, scaled.Width);
        Assert.Equal(600, scaled.Height);
    }
}
