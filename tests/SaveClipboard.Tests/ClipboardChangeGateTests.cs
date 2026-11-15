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
    public void 符合序號時只消費一次()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(42);

        Assert.True(gate.TryConsume(42));
        Assert.False(gate.TryConsume(42));
    }

    [Fact]
    public void 不同序號時不消費()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(42);

        Assert.False(gate.TryConsume(43));
    }

    [Fact]
    public void 序號為零時不消費()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(0);

        Assert.False(gate.TryConsume(0));
    }

    [Fact]
    public void 連續設定以最後一次為準()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(42);
        gate.ExpectOwnChange(43);

        Assert.False(gate.TryConsume(42));
        Assert.True(gate.TryConsume(43));
    }

    [Fact]
    public void Clear_取消期待值()
    {
        var gate = new ClipboardChangeGate();
        gate.ExpectOwnChange(42);
        gate.Clear();

        Assert.False(gate.TryConsume(42));
    }
}
