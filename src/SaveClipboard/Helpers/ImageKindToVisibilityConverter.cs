using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Com.VibeCoding.SaveClipboard.Helpers;

public sealed class ImageKindToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Models.ClipboardEntryKind kind && kind == Models.ClipboardEntryKind.Image
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
