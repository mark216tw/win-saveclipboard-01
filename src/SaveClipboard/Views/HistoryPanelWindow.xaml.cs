using System.Collections.Specialized;
using System.ComponentModel;
using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Com.VibeCoding.SaveClipboard.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Com.VibeCoding.SaveClipboard.Views;

public sealed partial class HistoryPanelWindow : Window
{
    private const int MinPanelWidthDip = 320;
    private const int MinPanelHeightDip = 400;
    private const int ShowForegroundProtectionMs = 2000;
    private const int ForegroundReassertDelayMs = 120;
    private const int PauseRefreshIntervalSeconds = 30;

    private readonly ILogger<HistoryPanelWindow> _logger;
    private readonly WindowPlacementService _placement;
    private readonly DialogService _dialog;
    private readonly MemoryTrimService _memoryTrim;
    private bool _isDraggingHeader;
    private uint _dragPointerId;
    private Windows.Foundation.Point _dragStartScreen;
    private Windows.Graphics.PointInt32 _dragStartWindow;
    private bool _allowHide = true;
    private bool _settingsOpen;
    private SettingsView? _settingsView;
    private bool _enforcingMinimumSize;
    private bool _suppressSearchChange;
    private bool _isExiting;
    private bool _welcomeShown;
    private DateTimeOffset _lastShowRequestAt;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _toastTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _foregroundTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _pauseRefreshTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _undoTimer;
    private IReadOnlyList<DeletedClipboardEntry> _deletedForUndo = [];

    public HistoryPanelWindow(
        HistoryPanelViewModel viewModel,
        ThemeService themeService,
        MemoryTrimService memoryTrim,
        Func<Windows.Graphics.RectInt32?> getTrayRect,
        ILogger<WindowPlacementService> placementLogger,
        ILogger<HistoryPanelWindow> logger)
    {
        ViewModel = viewModel;
        _memoryTrim = memoryTrim;
        _logger = logger;
        InitializeComponent();
        RootGrid.DataContext = viewModel;
        Title = WindowTitle;
        _dialog = new DialogService(RootGrid, value => _allowHide = value, logger);

        ConfigurePresenter();
        ConfigureIcon();
        _placement = new WindowPlacementService(AppWindow, getTrayRect, placementLogger);
        _placement.Apply(ViewModel.HasStoredPanelBounds, ViewModel.StoredPanelBounds, ViewModel.StoredPanelDpi);
        ConfigureEmptyState();

        themeService.Attach(RootGrid);
        ViewModel.Settings.ApplyStoredTheme();

        ViewModel.CloseRequested += HidePanel;
        ViewModel.CopyCompleted += OnCopyCompleted;
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

    public bool IsPanelVisible => AppWindow.IsVisible;

    public void ShowNearTray()
    {
        _memoryTrim.CancelPending();

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool wasVisible = AppWindow.IsVisible;

        if (EntryList.ItemsSource is null)
        {
            EntryList.ItemsSource = ViewModel.VisibleItems;
        }

        if (!wasVisible)
        {
            ResetSearch();
            _placement.Apply(ViewModel.HasStoredPanelBounds, ViewModel.StoredPanelBounds, ViewModel.StoredPanelDpi);
        }

        _lastShowRequestAt = now;

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
        Activate();
        AppWindow.Hide();
        ReleaseResidentUiResources();
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
        _foregroundTimer?.Stop();

        AppWindow.Hide();
        ReleaseResidentUiResources();
        _logger.LogInformation("隱藏面板");
    }

    private void ReleaseResidentUiResources()
    {
        // 隱藏常駐時卸載清單容器與縮圖 BitmapImage 快取，並排程裁剪工作集
        EntryList.ItemsSource = null;
        BytesToImageSourceConverter.ClearCache();
        _memoryTrim.ScheduleTrim();
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

    /// <summary>標題列空白處按下：記錄起點並捕捉指標，開始拖曳移動視窗。</summary>
    private void HeaderBar_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(HeaderBar);
        if (!point.Properties.IsLeftButtonPressed
            || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _isDraggingHeader = true;
        _dragPointerId = e.Pointer.PointerId;
        _dragStartWindow = AppWindow.Position;
        _dragStartScreen = GetPointerScreenPosition(e);
        HeaderBar.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void HeaderBar_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingHeader || e.Pointer.PointerId != _dragPointerId)
        {
            return;
        }

        Windows.Foundation.Point current = GetPointerScreenPosition(e);
        int left = _dragStartWindow.X + (int)Math.Round(current.X - _dragStartScreen.X);
        int top = _dragStartWindow.Y + (int)Math.Round(current.Y - _dragStartScreen.Y);
        AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
        e.Handled = true;
    }

    private void HeaderBar_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingHeader && e.Pointer.PointerId == _dragPointerId)
        {
            EndHeaderDrag();
            e.Handled = true;
        }
    }

    private void HeaderBar_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndHeaderDrag();

    private void EndHeaderDrag()
    {
        if (!_isDraggingHeader)
        {
            return;
        }

        _isDraggingHeader = false;
        HeaderBar.ReleasePointerCaptures();

        // 拖曳結束即保存，避免未隱藏面板就結束程式時遺失新位置
        PersistBounds();
    }

    /// <summary>以 DIP 的指標位置換算成實體螢幕座標，跨 DPI 螢幕拖曳時仍能正確跟隨。</summary>
    private Windows.Foundation.Point GetPointerScreenPosition(PointerRoutedEventArgs e)
    {
        Windows.Foundation.Point position = e.GetCurrentPoint(null).Position;
        double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        Windows.Graphics.PointInt32 origin = AppWindow.Position;
        return new Windows.Foundation.Point(origin.X + (position.X * scale), origin.Y + (position.Y * scale));
    }

    /// <summary>按鈕等互動控制項要保留原本的點擊行為，不可當成拖曳區。</summary>
    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBox or ComboBox or ToggleSwitch or ListView)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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
            presenter.IsAlwaysOnTop = true;
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
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // 非本程式請求的啟用（例如工作列釘選圖示還原視窗）視為顯示請求。
        // 必須完整走一次顯示流程，只重設前景保護無法讓隱藏中的面板出現。
        // 面板已可見時只是一般取得焦點（例如使用者點回面板），不可再重跑顯示流程。
        if (!AppWindow.IsVisible
            && now - _lastShowRequestAt > TimeSpan.FromMilliseconds(ShowForegroundProtectionMs))
        {
            _lastShowRequestAt = now;
            _logger.LogInformation("外部啟用面板，改為顯示請求");
            DispatcherQueue.TryEnqueue(ShowNearTray);
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

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape)
        {
            return;
        }

        bool inputBlocked = _settingsOpen || !_allowHide;
        if (!inputBlocked && !string.IsNullOrEmpty(SearchBox.Text))
        {
            // 有搜尋條件時，第一次 Esc 先清除搜尋，再按一次才關閉面板
            ResetSearch();
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (_settingsOpen)
        {
            ViewModel.Settings.CloseCommand.Execute(null);
        }
        else if (!inputBlocked)
        {
            HidePanel();
        }

        e.Handled = true;
    }

    private async void EntryContent_Tapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ClipboardEntry entry })
            {
                await ViewModel.CopyCommand.ExecuteAsync(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "點擊項目複製失敗。");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HidePanel();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;
        ViewModel.Settings.BeginEdit();

        var view = new SettingsView(ViewModel.Settings);
        Grid.SetRowSpan(view, 4);
        RootGrid.Children.Add(view);
        _settingsView = view;
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
        if (_settingsView is not null)
        {
            RootGrid.Children.Remove(_settingsView);
            _settingsView = null;
        }

        _settingsOpen = false;

        // 設定可能改變「選取後不關閉」，重算底部提示
        ViewModel.RefreshRecordingState();
        RootGrid.Focus(FocusState.Programmatic);
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