using System.Runtime.InteropServices;
using Com.VibeCoding.SaveClipboard.Helpers;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardRetryTests
{
    [Fact]
    public async Task RunAsync_遇到COMException會重試直到成功()
    {
        int attempts = 0;

        await ClipboardRetry.RunAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new COMException("busy");
                }

                return Task.CompletedTask;
            },
            attempts: 3,
            delayMs: 1);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RunAsync_已取消時不執行操作()
    {
        int attempts = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ClipboardRetry.RunAsync(
                () =>
                {
                    attempts++;
                    return Task.CompletedTask;
                },
                cancellationToken: cancellation.Token));

        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task RunAsync_遇到OutOfMemoryException會重試()
    {
        int attempts = 0;

        await ClipboardRetry.RunAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new OutOfMemoryException("unreadable clipboard");
                }

                return Task.CompletedTask;
            },
            attempts: 3,
            delayMs: 1);

        Assert.Equal(3, attempts);
    }

    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(COMException))]
    [InlineData(typeof(System.IO.IOException))]
    public void IsClipboardBusy_辨識暫時性失敗(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        bool expected = exceptionType != typeof(System.IO.IOException);
        Assert.Equal(expected, ClipboardRetry.IsClipboardBusy(exception));
    }
}
