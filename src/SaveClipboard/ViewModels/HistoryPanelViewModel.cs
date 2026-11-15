using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;

namespace Com.VibeCoding.SaveClipboard.ViewModels;

public sealed partial class HistoryPanelViewModel : ObservableObject
{
    private readonly ClipboardHistoryService _history;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ClipboardRecordingState _recordingState;
    private string _storageSummary = AppStrings.Get("Storage_NoItems");

    public HistoryPanelViewModel(
        ClipboardHistoryService history,
        AppSettings settings,
        SettingsService settingsService,
        StartupService startupService,
        ThemeService themeService,
        IClipboardWriter clipboardWriter,
        HotkeyService hotkeyService,
        ClipboardRecordingState recordingState,
        HistoryPersistenceCoordinator persistenceCoordinator)
    {
        _history = history;
        _clipboardWriter = clipboardWriter;
        _settings = settings;
        _settingsService = settingsService;
        _recordingState = recordingState;
        _settings.AutoStart = startupService.IsEnabled();

        Settings = new SettingsViewModel(
            _settings,
            settingsService,
            startupService,
            history,
            themeService,
            hotkeyService,
            persistenceCoordinator);

        VisibleItems = new ObservableCollection<ClipboardEntry>();
        foreach (ClipboardEntry entry in _history.Items)
        {
            entry.PropertyChanged += OnEntryPropertyChanged;
        }

        _history.Items.CollectionChanged += OnItemsCollectionChanged;
        _recordingState.StateChanged += OnRecordingStateChanged;
        RebuildVisibleItems();
        UpdateStorageSummary();
    }

    public ObservableCollection<ClipboardEntry> Items => _history.Items;

    /// <summary>依搜尋字串過濾後，實際顯示在清單上的項目。</summary>
    public ObservableCollection<ClipboardEntry> VisibleItems { get; }

    public SettingsViewModel Settings { get; }

    public bool AutoHideEnabled => _settings.CloseBehavior == WindowCloseBehavior.Auto;

    public bool HasSelection => Items.Any(static item => item.IsSelected);

    public bool PasteOnSelect => _settings.PasteOnSelect;

    public bool AlwaysOnTop => _settings.AlwaysOnTop;

    public bool ShouldShowWelcome => !_settings.HasSeenWelcome;

    public void MarkWelcomeSeen()
    {
        if (_settings.HasSeenWelcome)
        {
            return;
        }

        _settings.HasSeenWelcome = true;
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 導引標記寫入失敗不影響使用，下次啟動可能再次顯示
        }
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public Microsoft.UI.Xaml.Visibility SearchClearVisibility =>
        HasSearchText ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public bool RecordingPaused => _recordingState.IsPaused;

    public string PauseActionText =>
        AppStrings.Get(RecordingPaused ? "Pause_ActionResume" : "Pause_ActionPause");

    public string PauseActionGlyph => RecordingPaused ? "\uE768" : "\uE769";

    public string FooterHint
    {
        get
        {
            if (RecordingPaused)
            {
                return _recordingState.PausedUntil is { } until
                    ? AppStrings.Format(
                        "Pause_FooterFormat",
                        Math.Max(1, (int)Math.Ceiling((until - DateTimeOffset.Now).TotalMinutes)))
                    : AppStrings.Get("Pause_Footer");
            }

            return PasteOnSelect
                ? $"{Settings.ShortcutText} · {AppStrings.Get("Footer_PasteOnSelectSuffix")}"
                : Settings.ShortcutText;
        }
    }

    public string StorageSummary => _storageSummary;

    public bool HasStoredPanelBounds => _settings.HasPanelBounds;

    public Windows.Graphics.RectInt32 StoredPanelBounds => new(
        _settings.PanelLeft,
        _settings.PanelTop,
        _settings.PanelWidth,
        _settings.PanelHeight);

    public int StoredPanelDpi => _settings.PanelDpi;

    public event Action? CloseRequested;

    public event Action<string>? CopyCompleted;

    /// <summary>項目已成功寫入剪貼簿，供視窗決定是否貼回前一個視窗。</summary>
    public event Action<ClipboardEntry>? ItemCopied;

    public void ResetSearch() => SearchText = string.Empty;

    /// <summary>設定儲存後或狀態變化後重算底部提示。</summary>
    public void RefreshRecordingState()
    {
        OnPropertyChanged(nameof(RecordingPaused));
        OnPropertyChanged(nameof(PauseActionText));
        OnPropertyChanged(nameof(PauseActionGlyph));
        OnPropertyChanged(nameof(FooterHint));
    }

    public void SavePanelBounds(int left, int top, int width, int height, int dpi)
    {
        if (_settings.HasPanelBounds
            && _settings.PanelLeft == left
            && _settings.PanelTop == top
            && _settings.PanelWidth == width
            && _settings.PanelHeight == height
            && _settings.PanelDpi == dpi)
        {
            return;
        }

        _settings.HasPanelBounds = true;
        _settings.PanelLeft = left;
        _settings.PanelTop = top;
        _settings.PanelWidth = width;
        _settings.PanelHeight = height;
        _settings.PanelDpi = dpi;

        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 視窗位置保存失敗不影響操作
        }
    }

    [RelayCommand]
    private void TogglePauseRecording() =>
        _recordingState.Toggle(ClipboardRecordingState.DefaultPauseDuration);

    [RelayCommand]
    private async Task CopyAsync(ClipboardEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        ClipboardWriteResult result;
        if (entry.Kind == ClipboardEntryKind.Text && entry.Text is not null)
        {
            result = await _clipboardWriter.WriteTextAsync(entry.Text);
        }
        else if (entry.Kind == ClipboardEntryKind.Image && entry.ImageBytes is not null)
        {
            result = await _clipboardWriter.WriteImageAsync(entry.ImageBytes);
        }
        else
        {
            return;
        }

        if (result.Status == ClipboardWriteStatus.Canceled)
        {
            return;
        }

        if (!result.Succeeded)
        {
            CopyCompleted?.Invoke(result.Message);
            return;
        }

        CopyCompleted?.Invoke(result.Message);
        ItemCopied?.Invoke(entry);
        if (PanelVisibilityRules.ShouldHideAfterCopy(AutoHideEnabled, AlwaysOnTop, PasteOnSelect))
        {
            CloseRequested?.Invoke();
        }
    }

    public void PinSelected()
    {
        List<ClipboardEntry> selected = Items.Where(static item => item.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        bool pin = !selected.All(static item => item.IsPinned);
        foreach (ClipboardEntry entry in selected)
        {
            _history.SetPinned(entry.Id, pin);
            entry.IsSelected = false;
        }
    }

    public IReadOnlyList<DeletedClipboardEntry> DeleteSelected()
    {
        List<(ClipboardEntry Entry, int Index)> selected = Items
            .Select(static (entry, index) => (Entry: entry, Index: index))
            .Where(static item => item.Entry.IsSelected)
            .OrderByDescending(static item => item.Index)
            .ToList();

        var deleted = new List<DeletedClipboardEntry>(selected.Count);
        foreach ((ClipboardEntry entry, int index) in selected)
        {
            if (_history.Remove(entry.Id))
            {
                deleted.Add(new DeletedClipboardEntry(entry, index));
            }
        }

        deleted.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        return deleted;
    }

    public DeletedClipboardEntry? DeleteEntry(ClipboardEntry entry)
    {
        int index = Items.IndexOf(entry);
        return _history.Remove(entry.Id) ? new DeletedClipboardEntry(entry, index) : null;
    }

    public void RestoreDeleted(IEnumerable<DeletedClipboardEntry> deleted)
    {
        foreach (DeletedClipboardEntry item in deleted.OrderBy(static item => item.Index))
        {
            _history.Restore(item.Entry, item.Index);
        }
    }

    [RelayCommand]
    private void ClearAll() => _history.ClearAll();

    partial void OnSearchTextChanged(string value)
    {
        RebuildVisibleItems();
        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(SearchClearVisibility));
    }

    private void RebuildVisibleItems() =>
        VisibleEntrySynchronizer.Sync(VisibleItems, Items, SearchText);

    private void OnRecordingStateChanged() => RefreshRecordingState();

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        RebuildVisibleItems();
        UpdateStorageSummary();
        OnPropertyChanged(nameof(HasSelection));
    }

    private void UpdateStorageSummary()
    {
        long imageBytes = Items.Sum(static item =>
            (long)(item.ImageBytes?.Length ?? 0) + (item.ThumbnailBytes?.Length ?? 0));
        _storageSummary = Items.Count == 0
            ? AppStrings.Get("Storage_NoItems")
            : AppStrings.Format("Storage_SummaryFormat", Items.Count, FormatImageBytes(imageBytes));
        OnPropertyChanged(nameof(StorageSummary));
    }

    private static string FormatImageBytes(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return AppStrings.Format("Storage_Megabytes", bytes / (1024.0 * 1024.0));
        }

        return AppStrings.Format("Storage_Kilobytes", bytes / 1024.0);
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipboardEntry.IsSelected))
        {
            OnPropertyChanged(nameof(HasSelection));
        }
    }
}
