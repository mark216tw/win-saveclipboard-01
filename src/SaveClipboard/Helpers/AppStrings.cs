using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 程式碼端的多語系字串存取。找不到資源或資源系統不可用時回傳 key，
/// 讓 UI 至少不會顯示空白。已解析的字串會快取，避免重複跨 WinRT COM 查詢。
/// </summary>
public static class AppStrings
{
    private static readonly ResourceManager? Manager = CreateManager();
    private static readonly ResourceContext? Context = Manager?.CreateResourceContext();
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Get(string key)
    {
        if (Manager is null || Context is null)
        {
            return key;
        }

        return Cache.GetOrAdd(key, static resourceKey =>
        {
            try
            {
                return Manager!.MainResourceMap
                    .GetValue($"Resources/{resourceKey}", Context!)
                    .ValueAsString;
            }
            catch (Exception)
            {
                return resourceKey;
            }
        });
    }

    public static string Format(string key, params object?[] arguments)
    {
        string format = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static ResourceManager? CreateManager()
    {
        try
        {
            return new ResourceManager();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
