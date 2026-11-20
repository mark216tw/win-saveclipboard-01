using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 將 byte[]（PNG）轉換為 BitmapImage。空資料回傳 null。
/// 以位元組陣列參考快取，避免 ListView 容器回收時重複解碼縮圖；
/// 面板隱藏常駐時可呼叫 <see cref="ClearCache"/> 釋放解碼後的影像資源。
/// </summary>
public sealed class BytesToImageSourceConverter : IValueConverter
{
    private const int ThumbnailDecodePixelWidth = 72;
    private static readonly ConditionalWeakTable<byte[], BitmapImage> Cache = new();

    /// <summary>清空已解碼縮圖快取，讓常駐期間不保留額外的影像記憶體。</summary>
    public static void ClearCache() => Cache.Clear();

    public static BitmapImage? ConvertBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        return Cache.GetValue(bytes, static data =>
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = ThumbnailDecodePixelWidth,
            };
            using var memoryStream = new MemoryStream(data, writable: false);
            using var randomAccessStream = memoryStream.AsRandomAccessStream();
            image.SetSource(randomAccessStream);
            return image;
        });
    }

    public object? Convert(object value, Type targetType, object parameter, string language) =>
        ConvertBytes(value as byte[]);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
