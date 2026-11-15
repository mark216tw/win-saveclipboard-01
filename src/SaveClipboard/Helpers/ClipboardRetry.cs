using System.Runtime.InteropServices;

namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 剪貼簿可能被其他程式短暫佔用（CLIPBRD_E_CANT_OPEN），以短延遲重試降低漏記與複製失敗。
/// </summary>
internal static class ClipboardRetry
{
    private const int DefaultAttempts = 3;
    private const int DefaultDelayMs = 40;
    private const int ClipboardBusyHResult = unchecked((int)0x800401D0);

    /// <summary>
    /// WinRT 的剪貼簿忙碌可能以 COMException、OutOfMemoryException（0x8007000E，
    /// 常見於剪貼簿空白、來源程式已結束或格式暫時無法解析）或特定 HRESULT 呈現。
    /// </summary>
    public static bool IsClipboardBusy(Exception exception) =>
        exception is COMException or OutOfMemoryException
        || exception.HResult == ClipboardBusyHResult;

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> operation,
        int attempts = DefaultAttempts,
        int delayMs = DefaultDelayMs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation().ConfigureAwait(true);
            }
            catch (Exception ex) when (attempt < attempts && IsClipboardBusy(ex))
            {
                await Task.Delay(delayMs * attempt, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    public static async Task RunAsync(
        Func<Task> operation,
        int attempts = DefaultAttempts,
        int delayMs = DefaultDelayMs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await operation().ConfigureAwait(true);
                return;
            }
            catch (Exception ex) when (attempt < attempts && IsClipboardBusy(ex))
            {
                await Task.Delay(delayMs * attempt, cancellationToken).ConfigureAwait(true);
            }
        }
    }
}
