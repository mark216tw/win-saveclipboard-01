using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Services;
using Com.VibeCoding.SaveClipboard.ViewModels;
using Com.VibeCoding.SaveClipboard.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Com.VibeCoding.SaveClipboard;

public partial class App : Application
{
    private const string MutexName = @"Local\SaveClipboard.SingleInstance";
    private const uint SecondInstanceTimeoutMs = 3000;

    private Mutex? _mutex;
    private IHost? _host;
    private AppLifetime? _lifetime;
    private HelperWindow? _helperWindow;
    private ClipboardListenerService? _clipboardListener;
    private ClipboardRecordingState? _recordingState;
    private HotkeyService? _hotkey;
    private TrayIconService? _trayIcon;
    private HistoryPersistenceCoordinator? _persistenceCoordinator;
    private HistoryPanelWindow? _panel;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            DiagnosticLog.Write("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            DiagnosticLog.Write("TaskScheduler", e.Exception);
            e.SetObserved();
        };
        UnhandledException += OnUnhandledException;

        InitializeComponent();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        DiagnosticLog.Write("WinUI", e.Exception);

        // 只有已知可復原的例外才吞掉；其餘讓程式結束，避免在損毀狀態下繼續執行
        e.Handled = IsRecoverable(e.Exception);
    }

    private static bool IsRecoverable(Exception? exception) =>
        exception is System.Runtime.InteropServices.COMException
            or IOException
            or UnauthorizedAccessException;

    private static void WriteSecondInstanceLog(LogLevel level, string message)
    {
        try
        {
            using var provider = new DiagnosticsFileLoggerProvider();
            provider.CreateLogger(nameof(App)).Log(level, message);
        }
        catch
        {
            // 記錄失敗不影響第二實例結束流程
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 已有實例在執行：通知它顯示面板，並等它完成顯示、取得前景後才結束，
            // 避免非同步訊息造成前景交接競態（工作列第一次點擊只閃一下）。
            IntPtr existing = WaitForHelperWindow(SecondInstanceTimeoutMs);
            if (existing == IntPtr.Zero)
            {
                WriteSecondInstanceLog(LogLevel.Warning, "找不到既有實例的輔助視窗。");
            }
            else
            {
                // 讓既有實例能取得前景（第一個實例顯示面板時不會被誤判失焦而隱藏）
                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                IntPtr sent = NativeMethods.SendMessageTimeoutW(
                    existing,
                    NativeMethods.WM_SHOW_PANEL,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_BLOCK,
                    SecondInstanceTimeoutMs,
                    out _);
                if (sent == IntPtr.Zero)
                {
                    WriteSecondInstanceLog(LogLevel.Warning, "通知既有實例顯示面板逾時。");
                }
            }

            Environment.Exit(0);
            return;
        }

        _lifetime = new AppLifetime();
        CancellationToken lifetimeToken = _lifetime.Token;

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        // 剪貼簿事件與成功收錄都需要進到檔案日誌，才能事後判斷漏記原因；
        // 框架本身的 Debug 訊息過多，限制在 Warning 以上。
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Logging.AddProvider(new DiagnosticsFileLoggerProvider());
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<StartupService>();
        builder.Services.AddSingleton<HistoryPersistenceService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<ClipboardChangeGate>();
        builder.Services.AddSingleton<ClipboardRecordingState>();
        builder.Services.AddSingleton(sp =>
        {
            SettingsService settingsService = sp.GetRequiredService<SettingsService>();
            return settingsService.Load();
        });
        builder.Services.AddSingleton(sp =>
        {
            Models.AppSettings settings = sp.GetRequiredService<Models.AppSettings>();
            return new ClipboardHistoryService(settings.MaxItems);
        });
        _host = builder.Build();

        var settingsService = _host.Services.GetRequiredService<SettingsService>();
        var startupService = _host.Services.GetRequiredService<StartupService>();
        var themeService = _host.Services.GetRequiredService<ThemeService>();
        var changeGate = _host.Services.GetRequiredService<ClipboardChangeGate>();
        ClipboardRecordingState recordingState = _host.Services.GetRequiredService<ClipboardRecordingState>();
        _recordingState = recordingState;
        Models.AppSettings settings = _host.Services.GetRequiredService<Models.AppSettings>();
        ClipboardHistoryService history = _host.Services.GetRequiredService<ClipboardHistoryService>();
        var historyPersistence = _host.Services.GetRequiredService<HistoryPersistenceService>();
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();

        if (settings.PersistHistory)
        {
            history.Restore(historyPersistence.Load());
        }

        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("無法取得 UI 執行緒的 DispatcherQueue。");
        _persistenceCoordinator = new HistoryPersistenceCoordinator(
            history,
            historyPersistence,
            dispatcherQueue,
            settings.PersistHistory,
            loggerFactory.CreateLogger<HistoryPersistenceCoordinator>());

        bool autoStarted = IsAutoStartLaunch();
        MigrateAutoStartEntry(startupService);

        _helperWindow = new HelperWindow();
        _helperWindow.MessageReceived += OnHelperMessage;

        _hotkey = new HotkeyService(
            _helperWindow,
            TogglePanel,
            loggerFactory.CreateLogger<HotkeyService>());

        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _trayIcon = new TrayIconService(
            _helperWindow,
            iconPath,
            ShowPanel,
            ToggleRecordingPause,
            () => recordingState.IsPaused,
            ExitApplication,
            loggerFactory.CreateLogger<TrayIconService>());

        var clipboardWriter = new ClipboardWriterService(
            changeGate,
            lifetimeToken,
            loggerFactory.CreateLogger<ClipboardWriterService>());

        var viewModel = new HistoryPanelViewModel(
            history,
            settings,
            settingsService,
            startupService,
            themeService,
            clipboardWriter,
            _hotkey,
            recordingState,
            _persistenceCoordinator);
        Func<Windows.Graphics.RectInt32?> getTrayRect = () =>
        {
            if (_trayIcon is not null && _trayIcon.TryGetIconRect(out NativeMethods.RECT rect))
            {
                return new Windows.Graphics.RectInt32(
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top);
            }

            return null;
        };
        _panel = new HistoryPanelWindow(
            viewModel,
            themeService,
            getTrayRect,
            loggerFactory.CreateLogger<WindowPlacementService>(),
            loggerFactory.CreateLogger<HistoryPanelWindow>());

        _clipboardListener = new ClipboardListenerService(
            _helperWindow,
            history,
            changeGate,
            recordingState,
            lifetimeToken,
            loggerFactory.CreateLogger<ClipboardListenerService>(),
            loggerFactory.CreateLogger<ClipboardCaptureQueue>());

        recordingState.StateChanged += OnRecordingStateChanged;

        if (autoStarted)
        {
            // 開機自動啟動：只常駐系統匣；WinUI 3 仍需至少一個已啟用的 XAML 視窗維持生命週期。
            _panel.StartHidden();
        }
        else
        {
            // 使用者手動啟動（例如第一次點擊工作列釘選圖示）：直接顯示主面板。
            _panel.ShowNearTray();
        }
    }

    private static IntPtr WaitForHelperWindow(uint timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (true)
        {
            IntPtr hwnd = NativeMethods.FindWindowW(HelperWindow.ClassName, HelperWindow.WindowTitle);
            if (hwnd != IntPtr.Zero || Environment.TickCount64 >= deadline)
            {
                return hwnd;
            }

            // 第一個實例可能還在建立輔助視窗，短暫輪詢避免第二實例直接放棄
            Thread.Sleep(50);
        }
    }

    private static bool IsAutoStartLaunch()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 1; i < arguments.Length; i++)
        {
            if (string.Equals(arguments[i], StartupService.AutoStartArgument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void MigrateAutoStartEntry(StartupService startupService)
    {
        if (!startupService.IsEnabled() || startupService.HasAutoStartArgument())
        {
            return;
        }

        try
        {
            // 舊版登錄值缺少 --autostart，補寫後下次開機才能保持隱藏。
            startupService.SetEnabled(true);
            WriteSecondInstanceLog(LogLevel.Information, "已將開機啟動登錄值補上 --autostart。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 登錄檔無法寫入時不影響啟動
        }
    }

    private void ShowPanel() => _panel?.ShowNearTray();

    private void TogglePanel()
    {
        // 暫停可能因到期自動恢復，開啟面板時順便刷新系統列提示
        _trayIcon?.UpdatePausedState();
        _panel?.TogglePanel();
    }

    private void ToggleRecordingPause() =>
        _recordingState?.Toggle(ClipboardRecordingState.DefaultPauseDuration);

    private void OnRecordingStateChanged() => _trayIcon?.UpdatePausedState();

    private void OnHelperMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_SHOW_PANEL)
        {
            ShowPanel();
        }
    }

    private void ExitApplication()
    {
        // 先取消背景工作，避免非同步流程在視窗與服務釋放後仍存取控制項
        _lifetime?.Cancel();
        _panel?.PrepareForExit();

        // 結束前保存面板位置與大小，下次啟動即可還原
        _panel?.PersistBounds();

        if (_recordingState is not null)
        {
            _recordingState.StateChanged -= OnRecordingStateChanged;
        }

        _persistenceCoordinator?.Flush();
        _persistenceCoordinator?.Dispose();
        _persistenceCoordinator = null;

        _trayIcon?.Dispose();
        _trayIcon = null;
        _hotkey?.Dispose();
        _hotkey = null;
        _clipboardListener?.Dispose();
        _clipboardListener = null;
        _helperWindow?.Dispose();
        _helperWindow = null;
        _host?.Dispose();
        _host = null;
        _lifetime?.Dispose();
        _lifetime = null;
        Current.Exit();
    }
}
