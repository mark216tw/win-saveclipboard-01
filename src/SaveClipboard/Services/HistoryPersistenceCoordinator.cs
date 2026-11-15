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
    private bool _disposed;

    public HistoryPersistenceCoordinator(
        ClipboardHistoryService history,
        HistoryPersistenceService persistence,
        DispatcherQueue dispatcherQueue,
        bool enabled,
        ILogger<HistoryPersistenceCoordinator> logger)
    {
        _history = history;
        _persistence = persistence;
        _dispatcherQueue = dispatcherQueue;
        _enabled = enabled;
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

    public void Flush()
    {
        _timer.Stop();
        if (!_enabled)
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
        if (!_enabled || _disposed)
        {
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
