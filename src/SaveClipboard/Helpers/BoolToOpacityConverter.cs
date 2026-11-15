using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Com.VibeCoding.SaveClipboard.Helpers;

public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? 1.0 : 0.4;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
