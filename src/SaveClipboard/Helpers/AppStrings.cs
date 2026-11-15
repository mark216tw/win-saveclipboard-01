using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 程式碼端的多語系字串存取。找不到資源或資源系統不可用時回傳 key，
/// 讓 UI 至少不會顯示空白。
/// </summary>
public static class AppStrings
{
    private static readonly ResourceManager? Manager = CreateManager();
    private static readonly ResourceContext? Context = Manager?.CreateResourceContext();

    public static string Get(string key)
    {
        if (Manager is null || Context is null)
        {
            return key;
        }

        try
        {
            return Manager.MainResourceMap
                .GetValue($"Resources/{key}", Context)
                .ValueAsString;
        }
        catch (Exception)
        {
            return key;
        }
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
