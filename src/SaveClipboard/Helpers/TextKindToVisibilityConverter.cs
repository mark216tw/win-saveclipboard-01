using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Com.VibeCoding.SaveClipboard.Helpers;

public sealed class TextKindToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Models.ClipboardEntryKind kind && kind == Models.ClipboardEntryKind.Text
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
