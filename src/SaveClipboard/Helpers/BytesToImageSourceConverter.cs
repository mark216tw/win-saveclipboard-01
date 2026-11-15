using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 將 byte[]（PNG）轉換為 BitmapImage。空資料回傳 null。
/// 以位元組陣列參考快取，避免 ListView 容器回收時重複解碼縮圖。
/// </summary>
public sealed class BytesToImageSourceConverter : IValueConverter
{
    private static readonly ConditionalWeakTable<byte[], BitmapImage> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] bytes)
        {
            return null;
        }

        if (bytes.Length == 0)
        {
            return null;
        }
        return Cache.GetValue(bytes, static data =>
        {
            var image = new BitmapImage();
            image.SetSource(new MemoryStream(data).AsRandomAccessStream());
            return image;
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
