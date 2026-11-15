using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;

namespace Com.VibeCoding.SaveClipboard.ViewModels;

public sealed partial class ThemeSwatchViewModel : ObservableObject
{
    private readonly ThemeOption _option;

    public ThemeSwatchViewModel(ThemeOption option, bool isSelected)
    {
        _option = option;
        Id = option.Id;
        DisplayName = AppStrings.Get($"Theme_{option.Id}");
        PreviewBrush = new SolidColorBrush(option.LightColor);
        IsSelected = isSelected;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public SolidColorBrush PreviewBrush { get; }

    public void ApplyTheme(bool isDark) => PreviewBrush.Color = isDark ? _option.DarkColor : _option.LightColor;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Visibility SelectedVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(SelectedVisibility));
}
