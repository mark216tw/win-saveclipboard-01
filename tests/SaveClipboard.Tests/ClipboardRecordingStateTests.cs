using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardRecordingStateTests
{
    private DateTimeOffset _now = new(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(8));

    private ClipboardRecordingState CreateState() => new(() => _now);

    [Fact]
    public void 預設為記錄中()
    {
        ClipboardRecordingState state = CreateState();

        Assert.False(state.IsPaused);
        Assert.Null(state.PausedUntil);
    }

    [Fact]
    public void 暫停後為暫停狀態()
    {
        ClipboardRecordingState state = CreateState();

        state.Pause(TimeSpan.FromMinutes(30));

        Assert.True(state.IsPaused);
        Assert.Equal(_now.AddMinutes(30), state.PausedUntil);
    }

    [Fact]
    public void 到期後自動恢復()
    {
        ClipboardRecordingState state = CreateState();
        state.Pause(TimeSpan.FromMinutes(30));

        _now = _now.AddMinutes(31);

        Assert.False(state.IsPaused);
        Assert.Null(state.PausedUntil);
    }

    [Fact]
    public void 恢復後不再暫停()
    {
        ClipboardRecordingState state = CreateState();
        state.Pause(TimeSpan.FromMinutes(30));

        state.Resume();

        Assert.False(state.IsPaused);
    }

    [Fact]
    public void 重複恢復不會觸發事件()
    {
        ClipboardRecordingState state = CreateState();
        int raised = 0;
        state.StateChanged += () => raised++;

        state.Resume();
        state.Resume();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void 切換會依目前狀態切換()
    {
        ClipboardRecordingState state = CreateState();

        state.Toggle(TimeSpan.FromMinutes(30));
        Assert.True(state.IsPaused);

        state.Toggle(TimeSpan.FromMinutes(30));
        Assert.False(state.IsPaused);
    }

    [Fact]
    public void 狀態改變會通知()
    {
        ClipboardRecordingState state = CreateState();
        int raised = 0;
        state.StateChanged += () => raised++;

        state.Pause(TimeSpan.FromMinutes(30));
        state.Resume();

        Assert.Equal(2, raised);
    }
}
