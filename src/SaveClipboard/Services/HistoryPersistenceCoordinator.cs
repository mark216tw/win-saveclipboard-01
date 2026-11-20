using System.Collections.Specialized;
using System.ComponentModel;
using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 歷史變更時延遲寫入加密檔案，避免每次複製都同步寫磁碟。
/// 只在設定啟用持久化時運作；關閉時會刪除既有檔案。
/// </summary>
public sealed class HistoryPersistenceCoordinator : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private readonly ClipboardHistoryService _history;
    private readonly HistoryPersistenceService _persistence;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<HistoryPersistenceCoordinator> _logger;
    private readonly DispatcherQueueTimer _timer;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private bool _enabled;
    private bool _initialLoadPending;
    private bool _dirtyWhileLoading;
    private bool _suppressSave;
    private bool _disposed;

    public HistoryPersistenceCoordinator(
        ClipboardHistoryService history,
        HistoryPersistenceService persistence,
        DispatcherQueue dispatcherQueue,
        bool enabled,
        ILogger<HistoryPersistenceCoordinator> logger,
        bool initialLoadPending = false)
    {
        _history = history;
        _persistence = persistence;
        _dispatcherQueue = dispatcherQueue;
        _enabled = enabled;
        _initialLoadPending = enabled && initialLoadPending;
        _logger = logger;

        _timer = _dispatcherQueue.CreateTimer();
        _timer.Interval = SaveDelay;
        _timer.IsRepeating = false;
        _timer.Tick += OnTimerTick;

        _history.Items.CollectionChanged += OnCollectionChanged;
        foreach (ClipboardEntry entry in _history.Items)
        {
            entry.PropertyChanged += OnEntryPropertyChanged;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
        {
            return;
        }

        _enabled = enabled;
        if (enabled)
        {
            ScheduleSave(immediate: true);
        }
        else
        {
            _timer.Stop();
            _persistence.Delete();
        }
    }

    /// <summary>
    /// 在非同步讀取持久化歷史完成後套用還原動作，過程中不觸發多餘回寫；
    /// 若啟動期間已有新項目變動，還原後再排程寫入。
    /// </summary>
    public void CompleteInitialLoad(Action applyRestored)
    {
        ArgumentNullException.ThrowIfNull(applyRestored);

        if (_disposed)
        {
            return;
        }

        _suppressSave = true;
        try
        {
            applyRestored();
        }
        finally
        {
            _suppressSave = false;
            _initialLoadPending = false;
        }

        if (_dirtyWhileLoading && _enabled)
        {
            _dirtyWhileLoading = false;
            ScheduleSave(immediate: false);
        }
    }

    public void Flush()
    {
        _timer.Stop();
        if (!_enabled || _initialLoadPending)
        {
            return;
        }

        IReadOnlyList<ClipboardEntry> snapshot = _history.Items.ToList();
        _saveGate.Wait();
        try
        {
            _persistence.TrySave(snapshot);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _history.Items.CollectionChanged -= OnCollectionChanged;
        foreach (ClipboardEntry entry in _history.Items)
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ClipboardEntry entry in e.OldItems)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ClipboardEntry entry in e.NewItems)
            {
                entry.PropertyChanged += OnEntryPropertyChanged;
            }
        }

        ScheduleSave(immediate: false);
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClipboardEntry.IsPinned) or nameof(ClipboardEntry.CopiedAt))
        {
            ScheduleSave(immediate: false);
        }
    }

    private void ScheduleSave(bool immediate)
    {
        if (!_enabled || _disposed || _suppressSave)
        {
            return;
        }

        if (_initialLoadPending)
        {
            _dirtyWhileLoading = true;
            return;
        }

        _timer.Stop();
        if (immediate)
        {
            SaveSnapshot(_history.Items.ToList());
            return;
        }

        _timer.Start();
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_enabled && !_disposed)
        {
            SaveSnapshot(_history.Items.ToList());
        }
    }

    private void SaveSnapshot(IReadOnlyList<ClipboardEntry> snapshot)
    {
        _ = Task.Run(async () =>
        {
            await _saveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _persistence.TrySave(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "背景保存加密歷史失敗。");
            }
            finally
            {
                _saveGate.Release();
            }
        });
    }
}
