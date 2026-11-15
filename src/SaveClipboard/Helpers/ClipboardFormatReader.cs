using System.Runtime.InteropServices;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 讀取剪貼簿上的隱私標記格式。這些格式是簡單的 DWORD，
/// WinRT 的 DataPackageView 不一定能穩定還原型別，因此以 Win32 直接讀取。
/// 讀取失敗一律視為「沒有標記」，避免因無法判斷而全面停止記錄。
/// </summary>
internal static class ClipboardFormatReader
{
    public const string ExcludeMarkerFormat = "ExcludeClipboardContentFromMonitorProcessing";
    public const string CanIncludeInHistoryFormat = "CanIncludeInClipboardHistory";

    private const uint CF_UNICODETEXT = 13;

    public static bool HasFormat(IReadOnlyList<string> formats, string formatName) =>
        formats.Any(format => string.Equals(format, formatName, StringComparison.OrdinalIgnoreCase));

    public static bool? TryReadDword(IntPtr ownerWindow, string formatName, int attempts = 3, int delayMs = 10)
    {
        uint formatId = NativeMethods.RegisterClipboardFormatW(formatName);
        if (formatId == 0)
        {
            return null;
        }

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            if (!NativeMethods.OpenClipboard(ownerWindow))
            {
                if (attempt == attempts)
                {
                    return null;
                }

                Thread.Sleep(delayMs * attempt);
                continue;
            }

            try
            {
                IntPtr handle = NativeMethods.GetClipboardData(formatId);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr pointer = NativeMethods.GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.ReadInt32(pointer) != 0;
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            catch (Exception ex) when (ex is COMException or SEHException)
            {
                return null;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        return null;
    }

    /// <summary>
    /// WinRT 讀取失敗時的後援：直接以 Win32 讀取 CF_UNICODETEXT。
    /// 這條路徑不處理圖片，只確保常見的文字剪貼不會整筆遺失。
    /// </summary>
    public static string? TryReadUnicodeText(IntPtr ownerWindow, int attempts = 3, int delayMs = 10)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            if (!NativeMethods.OpenClipboard(ownerWindow))
            {
                if (attempt == attempts)
                {
                    return null;
                }

                Thread.Sleep(delayMs * attempt);
                continue;
            }

            try
            {
                IntPtr handle = NativeMethods.GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr pointer = NativeMethods.GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(pointer);
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            catch (Exception ex) when (ex is COMException or SEHException)
            {
                return null;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        return null;
    }
}
