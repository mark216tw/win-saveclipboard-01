namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 應用程式生命週期的取消來源。結束時先取消，讓背景剪貼簿處理與貼上流程能安全停止，
/// 再依序釋放其他服務，避免已釋放的視窗或控制項被非同步流程使用。
/// </summary>
public sealed class AppLifetime : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private bool _disposed;

    public CancellationToken Token => _cancellation.Token;

    public void Cancel()
    {
        if (!_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
        _cancellation.Dispose();
    }
}
