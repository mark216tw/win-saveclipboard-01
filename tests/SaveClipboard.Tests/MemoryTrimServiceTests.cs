using Com.VibeCoding.SaveClipboard.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SaveClipboard.Tests;

public class MemoryTrimServiceTests
{
    [Fact]
    public async Task ScheduleTrim_延遲後執行裁剪()
    {
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        using var service = new MemoryTrimService(
            cts.Token,
            NullLogger<MemoryTrimService>.Instance,
            delay: TimeSpan.FromMilliseconds(40),
            trimAction: () => executed.TrySetResult());

        service.ScheduleTrim();

        Task completed = await Task.WhenAny(executed.Task, Task.Delay(2000));
        Assert.Same(executed.Task, completed);
    }

    [Fact]
    public async Task ScheduleTrim_連續呼叫會防抖只執行一次()
    {
        int count = 0;
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        using var service = new MemoryTrimService(
            cts.Token,
            NullLogger<MemoryTrimService>.Instance,
            delay: TimeSpan.FromMilliseconds(80),
            trimAction: () =>
            {
                Interlocked.Increment(ref count);
                executed.TrySetResult();
            });

        service.ScheduleTrim();
        await Task.Delay(20);
        service.ScheduleTrim();
        await Task.Delay(20);
        service.ScheduleTrim();

        Task completed = await Task.WhenAny(executed.Task, Task.Delay(2000));
        Assert.Same(executed.Task, completed);
        await Task.Delay(120);
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact]
    public async Task CancelPending_取消待執行裁剪()
    {
        int count = 0;
        using var cts = new CancellationTokenSource();
        using var service = new MemoryTrimService(
            cts.Token,
            NullLogger<MemoryTrimService>.Instance,
            delay: TimeSpan.FromMilliseconds(60),
            trimAction: () => Interlocked.Increment(ref count));

        service.ScheduleTrim();
        service.CancelPending();

        await Task.Delay(150);
        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public async Task Dispose_停止後不再執行裁剪()
    {
        int count = 0;
        using var cts = new CancellationTokenSource();
        var service = new MemoryTrimService(
            cts.Token,
            NullLogger<MemoryTrimService>.Instance,
            delay: TimeSpan.FromMilliseconds(60),
            trimAction: () => Interlocked.Increment(ref count));

        service.ScheduleTrim();
        service.Dispose();
        service.ScheduleTrim();

        await Task.Delay(150);
        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public async Task CancellationToken取消時不執行裁剪()
    {
        int count = 0;
        using var cts = new CancellationTokenSource();
        using var service = new MemoryTrimService(
            cts.Token,
            NullLogger<MemoryTrimService>.Instance,
            delay: TimeSpan.FromMilliseconds(60),
            trimAction: () => Interlocked.Increment(ref count));

        service.ScheduleTrim();
        cts.Cancel();

        await Task.Delay(150);
        Assert.Equal(0, Volatile.Read(ref count));
    }
}
