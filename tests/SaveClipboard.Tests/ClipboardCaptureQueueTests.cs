using Com.VibeCoding.SaveClipboard.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardCaptureQueueTests
{
    private static ClipboardCaptureQueue CreateQueue(
        Func<uint, CancellationToken, Task> capture,
        TimeSpan? captureTimeout = null,
        int maxPending = ClipboardCaptureQueue.DefaultMaxPending,
        CancellationToken lifetimeToken = default) =>
        new(
            capture,
            lifetimeToken,
            NullLogger<ClipboardCaptureQueue>.Instance,
            captureTimeout ?? TimeSpan.FromSeconds(5),
            maxPending);

    private static async Task WaitUntilIdleAsync(ClipboardCaptureQueue queue, int timeoutMs = 3000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (!queue.IsProcessing && queue.PendingCount == 0)
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task 連續更新會依序全部處理()
    {
        var captured = new List<uint>();
        var firstStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        ClipboardCaptureQueue queue = CreateQueue(async (sequence, _) =>
        {
            if (sequence == 1)
            {
                firstStarted.TrySetResult();
                await release.Task;
            }

            lock (captured)
            {
                captured.Add(sequence);
            }
        });

        queue.Enqueue(1);
        await firstStarted.Task;
        for (uint sequence = 2; sequence <= 5; sequence++)
        {
            queue.Enqueue(sequence);
        }

        release.TrySetResult();
        await WaitUntilIdleAsync(queue);

        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, captured);
        Assert.Equal(0, queue.DroppedCount);
    }

    [Fact]
    public async Task 待處理超過上限時丟棄最舊序號()
    {
        var captured = new List<uint>();
        var firstStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        ClipboardCaptureQueue queue = CreateQueue(
            async (sequence, _) =>
            {
                if (sequence == 1)
                {
                    firstStarted.TrySetResult();
                    await release.Task;
                }

                lock (captured)
                {
                    captured.Add(sequence);
                }
            },
            maxPending: 4);

        queue.Enqueue(1);
        await firstStarted.Task;
        for (uint sequence = 2; sequence <= 8; sequence++)
        {
            queue.Enqueue(sequence);
        }

        Assert.Equal(3, queue.DroppedCount);

        release.TrySetResult();
        await WaitUntilIdleAsync(queue);

        Assert.Equal(new uint[] { 1, 5, 6, 7, 8 }, captured);
    }

    [Fact]
    public async Task 逾時後會繼續處理下一個()
    {
        var captured = new List<uint>();
        var neverCompletes = new TaskCompletionSource();
        var secondDone = new TaskCompletionSource();
        ClipboardCaptureQueue queue = CreateQueue(
            (sequence, _) =>
            {
                if (sequence == 1)
                {
                    return neverCompletes.Task;
                }

                lock (captured)
                {
                    captured.Add(sequence);
                }

                secondDone.TrySetResult();
                return Task.CompletedTask;
            },
            captureTimeout: TimeSpan.FromMilliseconds(50));

        queue.Enqueue(1);
        queue.Enqueue(2);
        await Task.WhenAny(secondDone.Task, Task.Delay(3000));
        await WaitUntilIdleAsync(queue);

        Assert.Equal(new uint[] { 2 }, captured);
    }

    [Fact]
    public void 生命週期取消後不再處理()
    {
        using var cancellation = new CancellationTokenSource();
        bool captured = false;
        ClipboardCaptureQueue queue = CreateQueue(
            (_, _) =>
            {
                captured = true;
                return Task.CompletedTask;
            },
            lifetimeToken: cancellation.Token);

        cancellation.Cancel();
        queue.Enqueue(1);

        Assert.False(captured);
        Assert.Equal(0, queue.PendingCount);
    }
}

