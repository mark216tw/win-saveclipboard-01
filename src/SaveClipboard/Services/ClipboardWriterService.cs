using Com.VibeCoding.SaveClipboard.Helpers;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Com.VibeCoding.SaveClipboard.Services;

public enum ClipboardWriteStatus
{
    Success,
    Busy,
    Failed,
    Canceled,
}

public readonly record struct ClipboardWriteResult(ClipboardWriteStatus Status, string Message)
{
    public bool Succeeded => Status == ClipboardWriteStatus.Success;
}

public interface IClipboardWriter
{
    Task<ClipboardWriteResult> WriteTextAsync(string text);

    Task<ClipboardWriteResult> WriteImageAsync(byte[] pngBytes);
}

/// <summary>
/// 將歷史項目寫回剪貼簿。重試、自我更新抑制、取消與例外處理集中在這裡，
/// ViewModel 只負責依結果更新 UI，不再直接碰 WinRT 剪貼簿或 Win32 序號。
/// </summary>
public sealed class ClipboardWriterService : IClipboardWriter
{
    private readonly ClipboardChangeGate _changeGate;
    private readonly CancellationToken _lifetimeToken;
    private readonly ILogger<ClipboardWriterService> _logger;

    public ClipboardWriterService(
        ClipboardChangeGate changeGate,
        CancellationToken lifetimeToken,
        ILogger<ClipboardWriterService> logger)
    {
        _changeGate = changeGate;
        _lifetimeToken = lifetimeToken;
        _logger = logger;
    }

    public async Task<ClipboardWriteResult> WriteTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_lifetimeToken.IsCancellationRequested)
        {
            return new ClipboardWriteResult(ClipboardWriteStatus.Canceled, string.Empty);
        }

        var package = new DataPackage();
        package.SetText(text);
        return await WriteAsync(package, AppStrings.Get("Copy_Text")).ConfigureAwait(true);
    }

    public async Task<ClipboardWriteResult> WriteImageAsync(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        if (_lifetimeToken.IsCancellationRequested)
        {
            return new ClipboardWriteResult(ClipboardWriteStatus.Canceled, string.Empty);
        }

        IRandomAccessStream? stream = null;
        try
        {
            stream = await CreateImageStreamAsync(pngBytes).ConfigureAwait(true);
            var package = new DataPackage();
            package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            return await WriteAsync(package, AppStrings.Get("Copy_Image")).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            stream?.Dispose();
            return new ClipboardWriteResult(ClipboardWriteStatus.Canceled, string.Empty);
        }
        catch (Exception ex)
        {
            stream?.Dispose();
            _logger.LogError(ex, "建立圖片剪貼簿內容失敗。");
            return new ClipboardWriteResult(ClipboardWriteStatus.Failed, AppStrings.Get("Copy_Failed"));
        }
    }

    private async Task<ClipboardWriteResult> WriteAsync(DataPackage package, string successMessage)
    {
        try
        {
            await ClipboardRetry.RunAsync(
                () =>
                {
                    _lifetimeToken.ThrowIfCancellationRequested();
                    Clipboard.SetContent(package);
                    Clipboard.Flush();

                    // 序號在 SetContent 當下遞增，必須在設定完成後讀取才不會比對到舊值
                    _changeGate.ExpectOwnChange(NativeMethods.GetClipboardSequenceNumber());
                    return Task.CompletedTask;
                },
                cancellationToken: _lifetimeToken).ConfigureAwait(true);

            return new ClipboardWriteResult(ClipboardWriteStatus.Success, successMessage);
        }
        catch (OperationCanceledException)
        {
            _changeGate.Clear();
            return new ClipboardWriteResult(ClipboardWriteStatus.Canceled, string.Empty);
        }
        catch (Exception ex) when (ClipboardRetry.IsClipboardBusy(ex))
        {
            _changeGate.Clear();
            _logger.LogWarning(ex, "設定剪貼簿失敗，可能正被其他程式佔用。");
            return new ClipboardWriteResult(ClipboardWriteStatus.Busy, AppStrings.Get("Copy_Busy"));
        }
        catch (Exception ex)
        {
            _changeGate.Clear();
            _logger.LogError(ex, "設定剪貼簿失敗。");
            return new ClipboardWriteResult(ClipboardWriteStatus.Failed, AppStrings.Get("Copy_Failed"));
        }
    }

    private async Task<IRandomAccessStream> CreateImageStreamAsync(byte[] imageBytes)
    {
        // 以 WinRT 串流寫入完整位元組，避免剪貼簿延後取用時資料失效
        var stream = new InMemoryRandomAccessStream();
        try
        {
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            _lifetimeToken.ThrowIfCancellationRequested();
            stream.Seek(0);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
