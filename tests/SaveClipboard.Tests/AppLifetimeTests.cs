using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class AppLifetimeTests
{
    [Fact]
    public void Cancel_會設定取消Token()
    {
        using var lifetime = new AppLifetime();

        Assert.False(lifetime.Token.IsCancellationRequested);

        lifetime.Cancel();

        Assert.True(lifetime.Token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_可重複呼叫()
    {
        using var lifetime = new AppLifetime();

        lifetime.Cancel();
        lifetime.Cancel();

        Assert.True(lifetime.Token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_會一併取消Token()
    {
        var lifetime = new AppLifetime();
        CancellationToken token = lifetime.Token;

        lifetime.Dispose();

        Assert.True(token.IsCancellationRequested);
    }
}
