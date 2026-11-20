using Com.VibeCoding.SaveClipboard.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Com.VibeCoding.SaveClipboard.Views;

/// <summary>
/// 設定頁使用者控制項。只在開啟設定時動態建立，關閉後即從視覺樹移除，
/// 避免啟動時解析大量設定控制項與常駐時保留未使用的視覺樹。
/// </summary>
public sealed partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    private void CloseSettings_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CloseCommand.Execute(null);

    private void SaveSettings_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveCommand.Execute(null);

    private void ThemeSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string themeId })
        {
            ViewModel.SelectTheme(themeId);
        }
    }
}
