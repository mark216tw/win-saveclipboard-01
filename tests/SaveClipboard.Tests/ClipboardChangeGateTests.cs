using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardChangeGateTests
{
    [Fact]
    public void 沒有期待值時不消費()
    {
        var gate = new ClipboardChangeGate();

        Assert.False(gate.TryConsume(42));
    }

    [Fact]
    public void 範圍內序號可重複消費()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(40, 42);

        Assert.True(gate.TryConsume(40));
        Assert.True(gate.TryConsume(41));
        Assert.True(gate.TryConsume(42));
        Assert.True(gate.TryConsume(42));
    }

    [Fact]
    public void 範圍外序號不消費()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(40, 42);

        Assert.False(gate.TryConsume(39));
        Assert.False(gate.TryConsume(43));
    }

    [Fact]
    public void 序號為零時不消費()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(0);
        Assert.False(gate.TryConsume(0));

        gate.ExpectOwnChange(40, 42);
        Assert.False(gate.TryConsume(0));
    }

    [Fact]
    public void 連續設定以最後一次為準()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(40, 42);
        gate.ExpectOwnChange(50, 52);

        Assert.False(gate.TryConsume(41));
        Assert.True(gate.TryConsume(51));
    }

    [Fact]
    public void Clear_取消期待值()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(40, 42);
        gate.Clear();

        Assert.False(gate.TryConsume(40));
        Assert.False(gate.TryConsume(42));
    }
}
