using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 將連續的 WM_CLIPBOARDUPDATE 依序排隊處理，避免只保留最後一次更新而漏記。
/// 每次擷取有逾時保護，擷取卡住時會記錄並繼續處理下一個序號，
/// 不會讓監聽器永久停在「處理中」狀態。
/// 必須在具有 SynchronizationContext 的 UI 執行緒使用，擷取回呼才會回到 UI 執行緒。
/// </summary>
public sealed class ClipboardCaptureQueue : IDisposable
{
    public const int DefaultMaxPending = 8;

    public static readonly TimeSpan DefaultCaptureTimeout = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly Queue<uint> _pending = new();
    private readonly Func<uint, CancellationToken, Task> _capture;
    private readonly CancellationToken _lifetimeToken;
    private readonly ILogger<ClipboardCaptureQueue> _logger;
    private readonly TimeSpan _captureTimeout;
    private readonly int _maxPending;
    private bool _processing;
    private bool _disposed;

    public ClipboardCaptureQueue(
        Func<uint, CancellationToken, Task> capture,
        CancellationToken lifetimeToken,
        ILogger<ClipboardCaptureQueue> logger,
        TimeSpan? captureTimeout = null,
        int maxPending = DefaultMaxPending)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(logger);

        _capture = capture;
        _lifetimeToken = lifetimeToken;
        _logger = logger;
        _captureTimeout = captureTimeout ?? DefaultCaptureTimeout;
        _maxPending = Math.Max(1, maxPending);
    }

    /// <summary>待處理佇列超過上限時，被丟棄的舊序號數量。</summary>
    public long DroppedCount { get; private set; }

    public bool IsProcessing
    {
        get
        {
            lock (_gate)
            {
                return _processing;
            }
        }
    }

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    public void Enqueue(uint sequence)
    {
        if (_disposed || _lifetimeToken.IsCancellationRequested)
        {
            return;
        }

        bool startProcessing;
        lock (_gate)
        {
            if (_disposed || _lifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (_pending.Count >= _maxPending)
            {
                uint dropped = _pending.Dequeue();
                DroppedCount++;
                _logger.LogWarning(
                    "待處理的剪貼簿更新過多，丟棄最舊的序號 {Dropped}（累計 {DroppedCount}）。",
                    dropped,
                    DroppedCount);
            }

            _pending.Enqueue(sequence);
            startProcessing = !_processing;
            if (startProcessing)
            {
                _processing = true;
            }
        }

        if (startProcessing)
        {
            _ = ProcessAsync();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending.Clear();
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            while (true)
            {
                uint sequence;
                lock (_gate)
                {
                    if (_disposed || _lifetimeToken.IsCancellationRequested || _pending.Count == 0)
                    {
                        break;
                    }

                    sequence = _pending.Dequeue();
                }

                try
                {
                    await RunWithTimeoutAsync(sequence).ConfigureAwait(true);
                }
                catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
                {
                    _logger.LogDebug("應用程式結束，停止處理剪貼簿序號 {Sequence}。", sequence);
                    break;
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning(ex, "剪貼簿擷取逾時（序號 {Sequence}），改為處理下一個更新。", sequence);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "讀取剪貼簿內容失敗（序號 {Sequence}）。", sequence);
                }
            }
        }
        finally
        {
            bool restart;
            lock (_gate)
            {
                _processing = false;
                restart = !_disposed
                    && !_lifetimeToken.IsCancellationRequested
                    && _pending.Count > 0;
                if (restart)
                {
                    _processing = true;
                }
            }

            if (restart)
            {
                // 結束前一刻又有新更新時，接續處理而不放掉
                _ = ProcessAsync();
            }
        }
    }

    private async Task RunWithTimeoutAsync(uint sequence)
    {
        Task capture = _capture(sequence, _lifetimeToken);
        Task timeout = Task.Delay(_captureTimeout, _lifetimeToken);
        Task completed = await Task.WhenAny(capture, timeout).ConfigureAwait(true);

        if (!ReferenceEquals(completed, capture))
        {
            _lifetimeToken.ThrowIfCancellationRequested();
            ObserveFailure(capture);
            throw new TimeoutException($"剪貼簿擷取超過 {_captureTimeout.TotalSeconds:0.###} 秒（序號 {sequence}）。");
        }

        await capture.ConfigureAwait(true);
    }

    private static void ObserveFailure(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
