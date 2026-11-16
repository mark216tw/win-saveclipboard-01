using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Microsoft.UI.Xaml;

namespace Com.VibeCoding.SaveClipboard.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly ClipboardHistoryService _history;
    private readonly ThemeService _themeService;
    private readonly HotkeyService _hotkeyService;
    private readonly HistoryPersistenceCoordinator _persistenceCoordinator;

    private AppSettings _snapshot;
    private string _themeId;
    private bool _suppressPreview;

    public SettingsViewModel(
        AppSettings settings,
        SettingsService settingsService,
        StartupService startupService,
        ClipboardHistoryService history,
        ThemeService themeService,
        HotkeyService hotkeyService,
        HistoryPersistenceCoordinator persistenceCoordinator)
    {
        _settings = settings;
        _settingsService = settingsService;
        _startupService = startupService;
        _history = history;
        _themeService = themeService;
        _hotkeyService = hotkeyService;
        _persistenceCoordinator = persistenceCoordinator;

        _snapshot = settings.Clone();
        _themeId = settings.ThemeId;
        Themes = new ObservableCollection<ThemeSwatchViewModel>(
            ThemeCatalog.All.Select(theme => new ThemeSwatchViewModel(
                theme,
                string.Equals(theme.Id, _themeId, StringComparison.OrdinalIgnoreCase))));

        LoadDraft(_settings);
        HasSettingsRecoveryNotice = settingsService.LastLoadStatus == SettingsLoadStatus.Recovered;
    }

    public ObservableCollection<ThemeSwatchViewModel> Themes { get; }

    public string ShortcutText => HotkeyService.DisplayText;

    public bool IsShortcutUnavailable => !_hotkeyService.IsRegistered;

    public Visibility ShortcutWarningVisibility =>
        IsShortcutUnavailable ? Visibility.Visible : Visibility.Collapsed;

    public string ShortcutStatusText => IsShortcutUnavailable
        ? AppStrings.Get("Shortcut_Unavailable")
        : AppStrings.Get("Shortcut_Available");

    public string AppName => AppStrings.Get("App_DisplayName");

    public string VersionText => AppStrings.Format("Version_Format", AppVersion);

    public string FileVersionText => AppStrings.Format("FileVersion_Format", FileVersion);

    private static string AppVersion =>
        typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private static string FileVersion =>
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(4)
        ?? "1.0.0.0";

    public event Action? EditCompleted;

    [ObservableProperty]
    public partial int MaxItemsDraft { get; set; }

    [ObservableProperty]
    public partial bool AutoStartDraft { get; set; }

    [ObservableProperty]
    public partial int DisplayModeIndex { get; set; }

    [ObservableProperty]
    public partial bool KeepPanelOpenAfterSelectDraft { get; set; }

    [ObservableProperty]
    public partial bool PersistHistoryDraft { get; set; }

    [ObservableProperty]
    public partial bool HasSettingsRecoveryNotice { get; set; }

    [ObservableProperty]
    public partial bool HasSaveError { get; set; }

    [ObservableProperty]
    public partial string SaveErrorText { get; set; } = string.Empty;

    public Visibility SaveErrorVisibility =>
        HasSaveError ? Visibility.Visible : Visibility.Collapsed;

    public string SettingsRecoveryNoticeText => _settingsService.LastBackupPath is { } backupPath
        ? AppStrings.Format("SettingsRecovery_NoticeWithBackup", Path.GetFileName(backupPath))
        : AppStrings.Get("SettingsRecovery_Notice");

    partial void OnHasSaveErrorChanged(bool value) => OnPropertyChanged(nameof(SaveErrorVisibility));

    public void BeginEdit()
    {
        HasSaveError = false;
        SaveErrorText = string.Empty;
        _snapshot = _settings.Clone();
        _themeId = _settings.ThemeId;

        _suppressPreview = true;
        try
        {
            LoadDraft(_settings);
        }
        finally
        {
            _suppressPreview = false;
        }

        UpdateThemeSelection();
        ApplyTheme(CurrentDisplayMode, _themeId);
    }

    public void ApplyStoredTheme() => ApplyTheme(_settings.DisplayMode, _settings.ThemeId);

    public void SelectTheme(ThemeSwatchViewModel? swatch) => SelectTheme(swatch?.Id);

    public void SelectTheme(string? themeId)
    {
        if (!ThemeCatalog.Contains(themeId)
            || string.Equals(themeId, _themeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _themeId = ThemeCatalog.Get(themeId).Id;
        UpdateThemeSelection();
        ApplyTheme(CurrentDisplayMode, _themeId);
    }

    [RelayCommand]
    private void IncreaseMaxItems() =>
        MaxItemsDraft = Math.Clamp(MaxItemsDraft + 10, AppSettings.MinMaxItems, AppSettings.MaxMaxItems);

    [RelayCommand]
    private void DecreaseMaxItems() =>
        MaxItemsDraft = Math.Clamp(MaxItemsDraft - 10, AppSettings.MinMaxItems, AppSettings.MaxMaxItems);

    [RelayCommand]
    private void ResetToDefaults()
    {
        MaxItemsDraft = AppSettings.DefaultMaxItems;
        AutoStartDraft = false;
        DisplayModeIndex = (int)DisplayMode.System;
        KeepPanelOpenAfterSelectDraft = false;
        PersistHistoryDraft = false;
        _themeId = ThemeCatalog.DefaultThemeId;
        UpdateThemeSelection();
        ApplyTheme(DisplayMode.System, _themeId);
        HasSaveError = false;
        SaveErrorText = string.Empty;
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DiagnosticLog.LogDirectory}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write(nameof(SettingsViewModel), ex);
            HasSaveError = true;
            SaveErrorText = AppStrings.Get("SettingsOpenLogFailed");
        }
    }

    [RelayCommand]
    private void Save()
    {
        _settings.MaxItems = Math.Clamp(MaxItemsDraft, AppSettings.MinMaxItems, AppSettings.MaxMaxItems);
        _settings.AutoStart = AutoStartDraft;
        _settings.DisplayMode = CurrentDisplayMode;
        _settings.ThemeId = _themeId;
        _settings.KeepPanelOpenAfterSelect = KeepPanelOpenAfterSelectDraft;
        _settings.PersistHistory = PersistHistoryDraft;
        _settings.Normalize();

        _history.MaxItems = _settings.MaxItems;

        var errors = new List<string>();
        bool settingsSaved = false;
        try
        {
            _settingsService.Save(_settings);
            settingsSaved = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            errors.Add(AppStrings.Get("SettingsSave_FileFailed"));
        }

        if (settingsSaved)
        {
            try
            {
                _startupService.SetEnabled(_settings.AutoStart);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                // 設定檔已寫入，但登錄檔無法更新時要讓使用者知道開機啟動沒有套用
                _settings.AutoStart = TryReadAutoStartState();
                AutoStartDraft = _settings.AutoStart;
                errors.Add(AppStrings.Get("SettingsSave_AutoStartFailed"));
            }

            _snapshot = _settings.Clone();
            _persistenceCoordinator.SetEnabled(_settings.PersistHistory);
        }

        ApplyTheme(_settings.DisplayMode, _settings.ThemeId);

        if (errors.Count > 0)
        {
            HasSaveError = true;
            SaveErrorText = string.Join(" ", errors);
            return;
        }

        HasSaveError = false;
        SaveErrorText = string.Empty;
        HasSettingsRecoveryNotice = false;
        EditCompleted?.Invoke();
    }

    private bool TryReadAutoStartState()
    {
        try
        {
            return _startupService.IsEnabled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        HasSaveError = false;
        SaveErrorText = string.Empty;
        _settings.CopyFrom(_snapshot);
        _suppressPreview = true;
        try
        {
            LoadDraft(_snapshot);
            _themeId = _snapshot.ThemeId;
        }
        finally
        {
            _suppressPreview = false;
        }

        UpdateThemeSelection();
        ApplyTheme(_snapshot.DisplayMode, _snapshot.ThemeId);
        _history.MaxItems = _snapshot.MaxItems;
        EditCompleted?.Invoke();
    }

    partial void OnDisplayModeIndexChanged(int value)
    {
        if (_suppressPreview)
        {
            return;
        }

        ApplyTheme(IndexToDisplayMode(value), _themeId);
    }

    private void LoadDraft(AppSettings settings)
    {
        MaxItemsDraft = settings.MaxItems;
        AutoStartDraft = settings.AutoStart;
        DisplayModeIndex = (int)settings.DisplayMode;
        KeepPanelOpenAfterSelectDraft = settings.KeepPanelOpenAfterSelect;
        PersistHistoryDraft = settings.PersistHistory;
    }

    private void ApplyTheme(DisplayMode displayMode, string themeId)
    {
        _themeService.Apply(displayMode, themeId);
        UpdateThemePreviews();
    }

    private void UpdateThemePreviews()
    {
        foreach (ThemeSwatchViewModel theme in Themes)
        {
            theme.ApplyTheme(_themeService.IsDark);
        }
    }

    private void UpdateThemeSelection()
    {
        foreach (ThemeSwatchViewModel theme in Themes)
        {
            theme.IsSelected = string.Equals(theme.Id, _themeId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private DisplayMode CurrentDisplayMode => IndexToDisplayMode(DisplayModeIndex);

    private static DisplayMode IndexToDisplayMode(int index) => index switch
    {
        1 => DisplayMode.Light,
        2 => DisplayMode.Dark,
        _ => DisplayMode.System,
    };
}
