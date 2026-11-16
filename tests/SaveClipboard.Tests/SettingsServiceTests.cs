using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SaveClipboard.Tests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    [Fact]
    public void Load_檔案不存在時回傳預設值()
    {
        var service = new SettingsService(_settingsPath);
        AppSettings settings = service.Load();

        Assert.Equal(AppSettings.DefaultMaxItems, settings.MaxItems);
        Assert.False(settings.AutoStart);
        Assert.Equal(DisplayMode.System, settings.DisplayMode);
        Assert.Equal(ThemeCatalog.DefaultThemeId, settings.ThemeId);
        Assert.False(settings.KeepPanelOpenAfterSelect);
    }

    [Fact]
    public void Save後Load_應保留設定()
    {
        var service = new SettingsService(_settingsPath);
        service.Save(new AppSettings
        {
            MaxItems = 40,
            AutoStart = true,
            DisplayMode = DisplayMode.Dark,
            ThemeId = "forest-green",
            KeepPanelOpenAfterSelect = true,
        });

        AppSettings loaded = service.Load();

        Assert.Equal(40, loaded.MaxItems);
        Assert.True(loaded.AutoStart);
        Assert.Equal(DisplayMode.Dark, loaded.DisplayMode);
        Assert.Equal("forest-green", loaded.ThemeId);
        Assert.True(loaded.KeepPanelOpenAfterSelect);
    }

    [Fact]
    public void Load_無效的主題與列舉值會回到預設()
    {
        File.WriteAllText(_settingsPath, """
            {
              "MaxItems": 30,
              "AutoStart": false,
              "DisplayMode": 99,
              "ThemeId": "not-a-theme"
            }
            """);

        var service = new SettingsService(_settingsPath);
        AppSettings loaded = service.Load();

        Assert.Equal(DisplayMode.System, loaded.DisplayMode);
        Assert.Equal(ThemeCatalog.DefaultThemeId, loaded.ThemeId);
    }

    [Fact]
    public void Load_舊版貼上與置頂欄位會被忽略()
    {
        File.WriteAllText(_settingsPath, """
            {
              "PasteOnSelect": false,
              "AutoAppendEnter": true,
              "AlwaysOnTop": true,
              "CloseBehavior": "Manual"
            }
            """);

        var service = new SettingsService(_settingsPath);
        AppSettings loaded = service.Load();

        Assert.False(loaded.KeepPanelOpenAfterSelect);
        Assert.Equal(SettingsLoadStatus.Ok, service.LastLoadStatus);
    }

    [Fact]
    public void Clone_複製所有設定()
    {
        var settings = new AppSettings
        {
            MaxItems = 20,
            AutoStart = true,
            DisplayMode = DisplayMode.Light,
            ThemeId = "teal",
            KeepPanelOpenAfterSelect = true,
        };

        AppSettings clone = settings.Clone();

        Assert.Equal(settings.MaxItems, clone.MaxItems);
        Assert.Equal(settings.AutoStart, clone.AutoStart);
        Assert.Equal(settings.DisplayMode, clone.DisplayMode);
        Assert.Equal(settings.ThemeId, clone.ThemeId);
        Assert.Equal(settings.KeepPanelOpenAfterSelect, clone.KeepPanelOpenAfterSelect);
    }

    [Fact]
    public void Load_超出範圍的數值會被修正()
    {
        var service = new SettingsService(_settingsPath);
        service.Save(new AppSettings { MaxItems = 99999 });

        AppSettings loaded = service.Load();

        Assert.Equal(AppSettings.MaxMaxItems, loaded.MaxItems);
    }

    [Fact]
    public void Load_損毀的檔案回傳預設值()
    {
        File.WriteAllText(_settingsPath, "{ not valid json");
        var service = new SettingsService(_settingsPath);

        AppSettings settings = service.Load();

        Assert.Equal(AppSettings.DefaultMaxItems, settings.MaxItems);
    }

    [Fact]
    public void Load_損毀的檔案會備份並標記為已復原()
    {
        File.WriteAllText(_settingsPath, "{ not valid json");
        var service = new SettingsService(_settingsPath);

        AppSettings settings = service.Load();

        Assert.Equal(AppSettings.DefaultMaxItems, settings.MaxItems);
        Assert.Equal(SettingsLoadStatus.Recovered, service.LastLoadStatus);
        Assert.NotNull(service.LastBackupPath);
        Assert.True(File.Exists(service.LastBackupPath));
        Assert.Equal("{ not valid json", File.ReadAllText(service.LastBackupPath!));
        Assert.False(File.Exists(_settingsPath));
    }

    [Fact]
    public void Load_檔案不存在時標記為未建立()
    {
        var service = new SettingsService(_settingsPath);

        service.Load();

        Assert.Equal(SettingsLoadStatus.Missing, service.LastLoadStatus);
        Assert.Null(service.LastBackupPath);
    }

    [Fact]
    public void Load_正常檔案標記為成功()
    {
        var service = new SettingsService(_settingsPath);
        service.Save(new AppSettings());

        service.Load();

        Assert.Equal(SettingsLoadStatus.Ok, service.LastLoadStatus);
    }

    [Fact]
    public void Load_備份後再次載入不會重複備份()
    {
        File.WriteAllText(_settingsPath, "{ not valid json");
        var service = new SettingsService(_settingsPath);
        service.Load();
        string? backupPath = service.LastBackupPath;

        service.Load();

        Assert.Equal(SettingsLoadStatus.Missing, service.LastLoadStatus);
        Assert.Null(service.LastBackupPath);
        Assert.True(File.Exists(backupPath));
        Assert.Single(Directory.GetFiles(_tempDir, "settings.corrupt-*.json"));
    }

    [Fact]
    public void Save_不留下暫存檔()
    {
        var service = new SettingsService(_settingsPath);

        service.Save(new AppSettings());

        Assert.True(File.Exists(_settingsPath));
        Assert.False(File.Exists(_settingsPath + ".tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
