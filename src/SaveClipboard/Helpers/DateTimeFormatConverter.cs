using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Com.VibeCoding.SaveClipboard.Helpers;

public sealed class DateTimeFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DateTimeOffset dto ? dto.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
