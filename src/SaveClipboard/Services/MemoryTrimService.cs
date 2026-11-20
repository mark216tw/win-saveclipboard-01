using System.Runtime;
using Com.VibeCoding.SaveClipboard.Helpers;
using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 面板隱藏進入常駐狀態後，延遲執行 GC 壓縮與工作集裁剪（SetProcessWorkingSetSize），
/// 降低背景常駐時的實體記憶體佔用，同時以防抖避免短時間重複開關造成頻繁 GC。
/// </summary>
public sealed class MemoryTrimService : IDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(1500);

    private readonly object _gate = new();
    private readonly TimeSpan _delay;
    private readonly Action _trimAction;
    private readonly CancellationToken _cancellationToken;
    private readonly ILogger<MemoryTrimService> _logger;
    private readonly Timer _timer;
    private int _trimming;
    private bool _disposed;

    public MemoryTrimService(
        CancellationToken cancellationToken,
        ILogger<MemoryTrimService> logger,
        TimeSpan? delay = null,
        Action? trimAction = null)
    {
        _cancellationToken = cancellationToken;
        _logger = logger;
        _delay = delay ?? DefaultDelay;
        _trimAction = trimAction ?? TrimProcessMemory;
        _timer = new Timer(OnTimerTick, state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void ScheduleTrim()
    {
        lock (_gate)
        {
            if (_disposed || _cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void CancelPending()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
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
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Dispose();
        }
    }

    private void OnTimerTick(object? state)
    {
        if (_disposed || _cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _trimming, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _trimAction();
            _logger.LogDebug("已完成常駐記憶體裁剪。");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "常駐記憶體裁剪失敗。");
        }
        finally
        {
            Volatile.Write(ref _trimming, 0);
        }
    }

    private static void TrimProcessMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), -1, -1);
    }
}
