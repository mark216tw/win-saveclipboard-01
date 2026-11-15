using System.Collections.Specialized;
using System.ComponentModel;
using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Com.VibeCoding.SaveClipboard.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Com.VibeCoding.SaveClipboard.Views;

public sealed partial class HistoryPanelWindow : Window
{
    private const int MinPanelWidthDip = 320;
    private const int MinPanelHeightDip = 400;
    private const int ShowForegroundProtectionMs = 2000;
    private const int MinimumFocusBeforeAutoHideMs = 500;
    private const int ForegroundReassertDelayMs = 120;
    private const int PasteTargetActivationDelayMs = 140;
    private const int PasteTargetSettleDelayMs = 90;
    private const int PauseRefreshIntervalSeconds = 30;

    private readonly ILogger<HistoryPanelWindow> _logger;
    private readonly CancellationToken _lifetimeToken;
    private readonly Action<string>? _showNotification;
    private readonly WindowPlacementService _placement;
    private readonly PasteAutomationService _paste;
    private readonly DialogService _dialog;
    private readonly KeyboardNavigationService _keyboard;
    private bool _allowHide = true;
    private bool _settingsOpen;
    private bool _everActivated;
    private bool _enforcingMinimumSize;
    private bool _suppressSearchChange;
    private bool _isExiting;
    private bool _welcomeShown;
    private bool _pasteInProgress;
    private int _selectionAnchor = -1;
    private DateTimeOffset _lastActivationAt;
    private DateTimeOffset _lastShowRequestAt;
    private DateTimeOffset _suppressDeactivationUntil;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _hideTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _toastTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _foregroundTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _pauseRefreshTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _undoTimer;
    private IReadOnlyList<DeletedClipboardEntry> _deletedForUndo = [];

    public HistoryPanelWindow(
        HistoryPanelViewModel viewModel,
        ThemeService themeService,
        CancellationToken lifetimeToken,
        Action<string>? showNotification,
        ForegroundWindowTracker foregroundTracker,
        Func<Windows.Graphics.RectInt32?> getTrayRect,
        ILogger<WindowPlacementService> placementLogger,
        ILogger<HistoryPanelWindow> logger)
    {
        ViewModel = viewModel;
        _logger = logger;
        _lifetimeToken = lifetimeToken;
        _showNotification = showNotification;
        _paste = new PasteAutomationService(
            () => WindowNative.GetWindowHandle(this),
            foregroundTracker,
            () => ViewModel?.PasteOnSelect ?? false,
            () => PanelVisibilityRules.KeepVisibleDuringPaste(ViewModel.AlwaysOnTop, ViewModel.PasteOnSelect),
            () => _isExiting,
            HidePanel,
            showNotification,
            lifetimeToken,
            logger);
        InitializeComponent();
        RootGrid.DataContext = viewModel;
        Title = WindowTitle;
        _dialog = new DialogService(RootGrid, value => _allowHide = value, logger);
        _keyboard = new KeyboardNavigationService(
            viewModel,
            EntryList,
            SearchBox,
            RootGrid,
            () => _settingsOpen || !_allowHide,
            () => _settingsOpen,
            ResetSearch,
            HidePanel,
            DeleteFromKeyboardAsync);

        ConfigurePresenter();
        ConfigureIcon();
        _placement = new WindowPlacementService(AppWindow, getTrayRect, placementLogger);
        _placement.Apply(ViewModel.HasStoredPanelBounds, ViewModel.StoredPanelBounds, ViewModel.StoredPanelDpi);
        ConfigureEmptyState();

        themeService.Attach(RootGrid);
        ViewModel.Settings.ApplyStoredTheme();

        ViewModel.CloseRequested += HidePanel;
        ViewModel.CopyCompleted += OnCopyCompleted;
        ViewModel.ItemCopied += OnItemCopied;
        ViewModel.Settings.EditCompleted += OnSettingsEditCompleted;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Activated += OnActivated;
    }

    /// <summary>結束程式前呼叫，讓 Alt+F4／視窗關閉不再被攔截成隱藏。</summary>
    public void PrepareForExit() => _isExiting = true;

    public const string WindowTitle = "SaveClipboard";

    public HistoryPanelViewModel ViewModel { get; }

    public void ShowNearTray()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool wasVisible = AppWindow.IsVisible;

        if (!wasVisible)
        {
            // 只在面板由隱藏轉為顯示時記錄，避免焦點回到自己時覆蓋真正的貼上目標
            _paste.CaptureTarget();
            ResetSearch();
            _placement.Apply(ViewModel.HasStoredPanelBounds, ViewModel.StoredPanelBounds, ViewModel.StoredPanelDpi);
        }

        _lastShowRequestAt = now;
        _suppressDeactivationUntil = now.AddMilliseconds(ShowForegroundProtectionMs);
        _everActivated = false;
        _lastActivationAt = default;
        _hideTimer?.Stop();

        AppWindow.Show();
        ForceForegroundWindow(hwnd);
        Activate();
        ScheduleForegroundReassert();
        FocusSearchBox();

        if (!_welcomeShown && ViewModel.ShouldShowWelcome)
        {
            _welcomeShown = true;
            DispatcherQueue.TryEnqueue(() => _ = ShowWelcomeAsync());
        }

        _logger.LogInformation(
            "顯示面板（原可見={WasVisible}，Items={ItemCount}，VisibleItems={VisibleCount}）",
            wasVisible,
            ViewModel.Items.Count,
            ViewModel.VisibleItems.Count);
    }

    /// <summary>
    /// 全域快捷鍵與系統列左鍵共用：面板隱藏時顯示，可見時收起。
    /// 設定頁開啟時先復原未儲存設定；對話方塊開啟中則忽略，避免視窗被藏起。
    /// </summary>
    public void TogglePanel()
    {
        if (!AppWindow.IsVisible)
        {
            ShowNearTray();
            return;
        }

        if (!_allowHide)
        {
            _logger.LogInformation("忽略切換：對話方塊開啟中");
            return;
        }

        if (_settingsOpen)
        {
            ViewModel.Settings.CloseCommand.Execute(null);
        }

        _logger.LogInformation("切換：隱藏面板");
        HidePanel();
    }

    /// <summary>
    /// 開機自動啟動用：建立生命週期所需的已啟用視窗後立即隱藏，
    /// 並記錄本次啟用為程式自身請求，避免被誤判為外部顯示請求而彈出面板。
    /// </summary>
    public void StartHidden()
    {
        _lastShowRequestAt = DateTimeOffset.UtcNow;
        _suppressDeactivationUntil = _lastShowRequestAt.AddMilliseconds(ShowForegroundProtectionMs);
        Activate();
        AppWindow.Hide();
        _logger.LogInformation("開機啟動，面板保持隱藏");
    }

    public void HidePanel()
    {
        PersistBounds();

        foreach (ClipboardEntry entry in ViewModel.Items.Where(static item => item.IsSelected))
        {
            entry.IsSelected = false;
        }

        HideCopyToast();
        HideUndoToast();
        _hideTimer?.Stop();
        _foregroundTimer?.Stop();
        _everActivated = false;
        _lastActivationAt = default;
        AppWindow.Hide();
        _logger.LogInformation("隱藏面板");
    }

    /// <summary>把目前視窗位置與大小寫回設定檔；僅在隱藏或結束時呼叫。</summary>
    public void PersistBounds()
    {
        Windows.Graphics.SizeInt32 size = AppWindow.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        Windows.Graphics.PointInt32 position = AppWindow.Position;
        int dpi = (int)NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this));
        ViewModel.SavePanelBounds(position.X, position.Y, size.Width, size.Height, dpi);
    }

    private void OnCopyCompleted(string message)
    {
        // 成功時自動隱藏模式會立刻關閉面板；失敗時面板仍開著，因此一律顯示提示。
        HideUndoToast();
        CopyToastText.Text = message;
        CopyToast.Visibility = Visibility.Visible;
        CopyToast.Opacity = 1;

        _toastTimer ??= DispatcherQueue.CreateTimer();
        _toastTimer.Interval = TimeSpan.FromMilliseconds(1500);
        _toastTimer.Tick -= OnToastTimerTick;
        _toastTimer.Tick += OnToastTimerTick;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void OnToastTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        HideCopyToast();
    }

    private void HideCopyToast()
    {
        _toastTimer?.Stop();
        CopyToast.Visibility = Visibility.Collapsed;
        CopyToast.Opacity = 0;
    }

    private void ShowUndoToast(IReadOnlyList<DeletedClipboardEntry> deleted)
    {
        if (deleted.Count == 0)
        {
            return;
        }

        _deletedForUndo = deleted;
        UndoToastText.Text = AppStrings.Format("Undo_DeletedFormat", deleted.Count);
        HideCopyToast();
        UndoToast.Visibility = Visibility.Visible;

        _undoTimer ??= DispatcherQueue.CreateTimer();
        _undoTimer.Interval = TimeSpan.FromSeconds(5);
        _undoTimer.IsRepeating = false;
        _undoTimer.Tick -= OnUndoTimerTick;
        _undoTimer.Tick += OnUndoTimerTick;
        _undoTimer.Stop();
        _undoTimer.Start();
    }

    private void OnUndoTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        HideUndoToast();
    }

    private void HideUndoToast()
    {
        _undoTimer?.Stop();
        UndoToast.Visibility = Visibility.Collapsed;
        _deletedForUndo = [];
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_deletedForUndo.Count == 0)
        {
            return;
        }

        ViewModel.RestoreDeleted(_deletedForUndo);
        HideUndoToast();
    }

    private void ConfigurePresenter()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = ViewModel.AlwaysOnTop;
        }
    }

    private void ApplyWindowPreferences()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = ViewModel.AlwaysOnTop;
        }
    }

    private void ConfigureIcon()
    {
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange && !_enforcingMinimumSize)
        {
            EnforceMinimumSize();
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting || Environment.HasShutdownStarted)
        {
            return;
        }

        // Alt+F4 或工作列關閉不應讓常駐程式失去面板；改為隱藏，托盤圖示仍可再開啟。
        args.Cancel = true;
        HidePanel();
        _logger.LogInformation("攔截視窗關閉，改為隱藏面板");
    }

    private void EnforceMinimumSize()
    {
        double scale = NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        int minWidth = (int)Math.Round(MinPanelWidthDip * scale);
        int minHeight = (int)Math.Round(MinPanelHeightDip * scale);

        Windows.Graphics.SizeInt32 size = AppWindow.Size;
        int width = Math.Max(size.Width, minWidth);
        int height = Math.Max(size.Height, minHeight);
        if (width == size.Width && height == size.Height)
        {
            return;
        }

        _enforcingMinimumSize = true;
        try
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }
        finally
        {
            _enforcingMinimumSize = false;
        }
    }

    private void ConfigureEmptyState()
    {
        UpdateEmptyState();
        ViewModel.VisibleItems.CollectionChanged += OnVisibleItemsChanged;
    }

    private void OnVisibleItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void UpdateEmptyState()
    {
        bool isEmpty = ViewModel.VisibleItems.Count == 0;
        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        EntryList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

        if (ViewModel.Items.Count == 0)
        {
            EmptyStateGlyph.Text = "\uE8C8";
            EmptyStateTitle.Text = AppStrings.Get("Empty_NoHistoryTitle");
            EmptyStateDescription.Text = AppStrings.Get("Empty_NoHistoryDescription");
        }
        else
        {
            EmptyStateGlyph.Text = "\uE721";
            EmptyStateTitle.Text = AppStrings.Get("Empty_NoMatchTitle");
            EmptyStateDescription.Text = AppStrings.Get("Empty_NoMatchDescription");
        }

        // 項目被刪除時原焦點元素會失效，將焦點收回網格避免面板因失焦而關閉。
        // 僅在焦點真的消失時才處理，否則在搜尋框輸入會被打斷。
        if (!_settingsOpen && AppWindow.IsVisible)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            XamlRoot? xamlRoot = RootGrid.XamlRoot;
            if (NativeMethods.GetForegroundWindow() == hwnd
                && xamlRoot is not null
                && FocusManager.GetFocusedElement(xamlRoot) is null)
            {
                RootGrid.Focus(FocusState.Programmatic);
            }
        }
    }

    private async void OnItemCopied(ClipboardEntry entry)
    {
        _pasteInProgress = true;
        try
        {
            await _paste.PasteAsync(entry);
        }
        catch (OperationCanceledException)
        {
            // 程式結束或面板關閉時取消貼上流程
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自動貼上流程失敗。");
        }
        finally
        {
            _pasteInProgress = false;
        }
    }
    private static void ForceForegroundWindow(IntPtr hwnd)
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        uint foregroundThread = foreground == IntPtr.Zero
            ? 0
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();
        bool attached = false;

        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attached = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.SetFocus(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // 面板失焦時，新的前景視窗就是使用者的下一個貼上目標（例如用 Alt+Tab 切到其他程式）
            _paste.UpdateTargetFromForeground();

            if (_settingsOpen
                || !_allowHide
                || !_everActivated
                || !ViewModel.AutoHideEnabled
                || _pasteInProgress
                || now < _suppressDeactivationUntil
                || now - _lastActivationAt < TimeSpan.FromMilliseconds(MinimumFocusBeforeAutoHideMs))
            {
                // 前景交接（第二實例結束、殼層還原視窗）造成的短暫失焦不關閉面板
                _logger.LogInformation(
                    "忽略失焦（保護中={Protected}, 取得前景={HasFocus}）",
                    now < _suppressDeactivationUntil,
                    _everActivated);
                return;
            }

            // 延遲隱藏：避免刪除項目等短暫焦點切換誤觸關閉
            _hideTimer ??= DispatcherQueue.CreateTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(220);
            _hideTimer.Tick -= OnHideTimerTick;
            _hideTimer.Tick += OnHideTimerTick;
            _hideTimer.Stop();
            _hideTimer.Start();
            _logger.LogInformation("面板失焦，排程隱藏");
        }
        else
        {
            _everActivated = true;
            _lastActivationAt = now;
            _hideTimer?.Stop();

            // 非本程式請求的啟用（例如工作列釘選圖示還原視窗）視為顯示請求。
            // 必須完整走一次顯示流程，只重設前景保護無法讓隱藏中的面板出現。
            // 面板已可見時只是一般取得焦點（例如使用者點回面板），不可再重跑顯示流程，
            // 否則會不斷重設 _everActivated 並反覆搶焦點。
            if (!AppWindow.IsVisible
                && now - _lastShowRequestAt > TimeSpan.FromMilliseconds(ShowForegroundProtectionMs))
            {
                _lastShowRequestAt = now;
                _suppressDeactivationUntil = now.AddMilliseconds(ShowForegroundProtectionMs);
                _logger.LogInformation("外部啟用面板，改為顯示請求");
                DispatcherQueue.TryEnqueue(ShowNearTray);
            }
        }
    }

    private void OnHideTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_allowHide && !_settingsOpen && !_pasteInProgress && ViewModel.AutoHideEnabled)
        {
            _logger.LogInformation("面板失焦後自動隱藏");
            HidePanel();
        }
    }

    private void ScheduleForegroundReassert()
    {
        _foregroundTimer ??= DispatcherQueue.CreateTimer();
        _foregroundTimer.Interval = TimeSpan.FromMilliseconds(ForegroundReassertDelayMs);
        _foregroundTimer.IsRepeating = false;
        _foregroundTimer.Tick -= OnForegroundTimerTick;
        _foregroundTimer.Tick += OnForegroundTimerTick;
        _foregroundTimer.Stop();
        _foregroundTimer.Start();
    }

    private void OnForegroundTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (!AppWindow.IsVisible)
        {
            return;
        }

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        if (NativeMethods.GetForegroundWindow() != hwnd)
        {
            ForceForegroundWindow(hwnd);
            _logger.LogInformation("重新取得面板前景");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistoryPanelViewModel.RecordingPaused))
        {
            UpdatePauseRefreshTimer();
        }
    }

    private void UpdatePauseRefreshTimer()
    {
        if (!ViewModel.RecordingPaused)
        {
            _pauseRefreshTimer?.Stop();
            return;
        }

        if (_pauseRefreshTimer is null)
        {
            _pauseRefreshTimer = DispatcherQueue.CreateTimer();
            _pauseRefreshTimer.Interval = TimeSpan.FromSeconds(PauseRefreshIntervalSeconds);
            _pauseRefreshTimer.IsRepeating = true;
            _pauseRefreshTimer.Tick += OnPauseRefreshTick;
        }

        if (!_pauseRefreshTimer.IsRunning)
        {
            _pauseRefreshTimer.Start();
        }
    }

    private void OnPauseRefreshTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        // 暫停到期會自動恢復，定期重算剩餘時間與底部提示
        ViewModel.RefreshRecordingState();
        UpdatePauseRefreshTimer();
    }

    private void FocusSearchBox() => DispatcherQueue.TryEnqueue(() =>
    {
        if (AppWindow.IsVisible && !_settingsOpen)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }
    });

    private void ResetSearch()
    {
        _suppressSearchChange = true;
        try
        {
            SearchBox.Text = string.Empty;
        }
        finally
        {
            _suppressSearchChange = false;
        }

        ViewModel.ResetSearch();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSearchChange)
        {
            return;
        }

        ViewModel.SearchText = SearchBox.Text;
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        ResetSearch();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            await _keyboard.HandleKeyDownAsync(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理面板鍵盤事件失敗。");
        }
    }

    private async Task DeleteFromKeyboardAsync()
    {
        if (ViewModel.Items.Any(static item => item.IsSelected))
        {
            await DeleteSelectedAsync();
            return;
        }

        ClipboardEntry? focused = _keyboard.GetFocusedEntry();
        if (focused is null)
        {
            return;
        }

        if (!await ConfirmAsync(AppStrings.Get("Dialog_DeleteOneTitle"), AppStrings.Get("Dialog_DeleteOneMessage"), AppStrings.Get("Dialog_DeleteButton")))
        {
            return;
        }

        DeletedClipboardEntry? deleted = ViewModel.DeleteEntry(focused);
        if (deleted is not null)
        {
            ShowUndoToast([deleted.Value]);
        }
    }

    private async void EntryContent_Tapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ClipboardEntry entry })
            {
                int index = ViewModel.VisibleItems.IndexOf(entry);
                if (KeyboardNavigationService.IsShiftDown())
                {
                    if (_selectionAnchor >= 0)
                    {
                        SelectRange(_selectionAnchor, index);
                        e.Handled = true;
                        return;
                    }
                }
                else if (KeyboardNavigationService.IsControlDown())
                {
                    entry.IsSelected = !entry.IsSelected;
                    _selectionAnchor = index;
                    e.Handled = true;
                    return;
                }

                _selectionAnchor = index;
                await ViewModel.CopyCommand.ExecuteAsync(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "點擊項目複製失敗。");
        }
    }

    private void SelectRange(int anchor, int index)
    {
        if (anchor < 0 || index < 0)
        {
            return;
        }

        int start = Math.Min(anchor, index);
        int end = Math.Max(anchor, index);
        for (int i = start; i <= end && i < ViewModel.VisibleItems.Count; i++)
        {
            ViewModel.VisibleItems[i].IsSelected = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HidePanel();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsOpen = true;
        _hideTimer?.Stop();
        ViewModel.Settings.BeginEdit();

        // InfoBar 的預設樣式在目前 WinUI 版本會缺少資源鍵，改用自訂提示列
        SettingsRecoveryBanner.Visibility = ViewModel.Settings.HasSettingsRecoveryNotice
            ? Visibility.Visible
            : Visibility.Collapsed;

        SettingsPage.Visibility = Visibility.Visible;
    }

    private async Task ShowWelcomeAsync()
    {
        if (!AppWindow.IsVisible)
        {
            return;
        }

        // 第一次顯示時 XamlRoot 可能尚未建立，稍等配置完成再開對話框
        for (int attempt = 0; attempt < 20 && RootGrid.XamlRoot is null; attempt++)
        {
            await Task.Delay(50);
        }

        if (RootGrid.XamlRoot is null)
        {
            return;
        }

        try
        {
            await _dialog.ShowAsync(
                AppStrings.Get("Welcome_Title"),
                AppStrings.Get("Welcome_Message"),
                AppStrings.Get("Welcome_Start"));
        }
        finally
        {
            ViewModel.MarkWelcomeSeen();
        }
    }

    private void OnSettingsEditCompleted()
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        _settingsOpen = false;

        // 設定可能改變「選取即貼上」與置頂，重算提示並套用視窗設定
        ViewModel.RefreshRecordingState();
        ApplyWindowPreferences();
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Settings.CloseCommand.Execute(null);

    private void SaveSettings_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Settings.SaveCommand.Execute(null);

    private void ThemeSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string themeId })
        {
            ViewModel.Settings.SelectTheme(themeId);
        }
    }

    private void PinSelected_Click(object sender, RoutedEventArgs e) => ViewModel.PinSelected();

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await DeleteSelectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除選取項目失敗。");
        }
    }

    private async Task DeleteSelectedAsync()
    {
        int count = ViewModel.Items.Count(static item => item.IsSelected);
        if (count == 0)
        {
            return;
        }

        if (!await ConfirmAsync(AppStrings.Format("Dialog_DeleteManyTitleFormat", count), AppStrings.Get("Dialog_DeleteManyMessage"), AppStrings.Get("Dialog_DeleteButton")))
        {
            return;
        }

        IReadOnlyList<DeletedClipboardEntry> deleted = ViewModel.DeleteSelected();
        ShowUndoToast(deleted);
    }

    private async void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await ConfirmAsync(AppStrings.Get("Dialog_ClearAllTitle"), AppStrings.Get("Dialog_ClearAllMessage"), AppStrings.Get("Dialog_ClearAllButton")))
            {
                return;
            }

            ViewModel.ClearAllCommand.Execute(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除全部歷史失敗。");
        }
    }

    private Task<bool> ConfirmAsync(string title, string message, string primaryText) =>
        _dialog.ConfirmAsync(title, message, primaryText, AppStrings.Get("Dialog_Cancel"));
}