using Com.VibeCoding.SaveClipboard.Helpers;
using Com.VibeCoding.SaveClipboard.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 監聽系統剪貼簿（WM_CLIPBOARDUPDATE），將文字與圖片寫入歷史。
/// 每個更新以序號排隊處理，避免連續複製時只留下最後一筆；
/// 圖片解碼、重新編碼、縮圖與雜湊在背景執行緒處理，完成後才回到 UI 執行緒加入歷史。
/// WinRT 讀取失敗時會退回 Win32 CF_UNICODETEXT，降低文字整筆漏記的機率。
/// 僅保存於記憶體，不寫入磁碟。
/// </summary>
public sealed class ClipboardListenerService : IDisposable
{
    private const uint ThumbnailWidth = 96;
    private const int MaxTextChars = 2 * 1024 * 1024;

    private readonly HelperWindow _window;
    private readonly ClipboardHistoryService _history;
    private readonly ClipboardChangeGate _changeGate;
    private readonly ClipboardRecordingState _recordingState;
    private readonly ClipboardCaptureQueue _captureQueue;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<ClipboardListenerService> _logger;
    private bool _disposed;

    public ClipboardListenerService(
        HelperWindow window,
        ClipboardHistoryService history,
        ClipboardChangeGate changeGate,
        ClipboardRecordingState recordingState,
        CancellationToken lifetimeToken,
        ILogger<ClipboardListenerService> logger,
        ILogger<ClipboardCaptureQueue> captureQueueLogger)
    {
        _window = window;
        _history = history;
        _changeGate = changeGate;
        _recordingState = recordingState;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("無法取得 UI 執行緒的 DispatcherQueue。");
        _captureQueue = new ClipboardCaptureQueue(CaptureOnUiThreadAsync, lifetimeToken, captureQueueLogger);

        _window.MessageReceived += OnWindowMessage;

        if (!NativeMethods.AddClipboardFormatListener(_window.Handle))
        {
            _logger.LogError("AddClipboardFormatListener 失敗，剪貼簿監聽未啟動。");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _captureQueue.Dispose();
        _window.MessageReceived -= OnWindowMessage;
        NativeMethods.RemoveClipboardFormatListener(_window.Handle);
    }

    private void OnWindowMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != NativeMethods.WM_CLIPBOARDUPDATE)
        {
            return;
        }

        if (_recordingState.IsPaused)
        {
            _logger.LogDebug("記錄已暫停，略過剪貼簿更新");
            return;
        }

        uint sequence = NativeMethods.GetClipboardSequenceNumber();
        if (_changeGate.TryConsume(sequence))
        {
            _logger.LogDebug("略過本程式設定剪貼簿造成的更新（序號 {Sequence}）", sequence);
            return;
        }

        _logger.LogDebug("收到 WM_CLIPBOARDUPDATE（序號 {Sequence}）", sequence);
        _captureQueue.Enqueue(sequence);
    }

    private Task CaptureOnUiThreadAsync(uint sequence, CancellationToken cancellationToken)
    {
        // 剪貼簿與歷史集合都只能在 UI 執行緒存取；佇列的續行若不在 UI 執行緒就轉交。
        if (_dispatcherQueue.HasThreadAccess)
        {
            return CaptureAsync(sequence, cancellationToken);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await CaptureAsync(sequence, cancellationToken);
                completion.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            completion.TrySetCanceled(cancellationToken);
        }

        return completion.Task;
    }

    private async Task CaptureAsync(uint sequence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("開始處理剪貼簿序號 {Sequence}", sequence);

        DataPackageView content;
        try
        {
            content = await ClipboardRetry.RunAsync(
                static () => Task.FromResult(Clipboard.GetContent()),
                cancellationToken: cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (IsReadUnavailable(ex))
        {
            _logger.LogDebug(ex, "取得剪貼簿內容失敗（序號 {Sequence}），改以 Win32 讀取文字。", sequence);
            await CaptureTextFallbackAsync(sequence, "取得剪貼簿內容失敗", cancellationToken).ConfigureAwait(true);
            return;
        }

        try
        {
            await CaptureContentAsync(content, sequence, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (IsReadUnavailable(ex))
        {
            _logger.LogDebug(ex, "讀取剪貼簿內容失敗（序號 {Sequence}），改以 Win32 讀取文字。", sequence);
            await CaptureTextFallbackAsync(sequence, "讀取剪貼簿內容失敗", cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task CaptureContentAsync(
        DataPackageView content,
        uint sequence,
        CancellationToken cancellationToken)
    {
        (ClipboardSkipReason skipReason, bool formatsKnown) =
            await EvaluatePrivacyAsync(content, cancellationToken).ConfigureAwait(true);
        if (skipReason != ClipboardSkipReason.None)
        {
            _logger.LogInformation("略過剪貼簿內容（{Reason}，序號 {Sequence}）。", skipReason, sequence);
            return;
        }

        if (!formatsKnown)
        {
            // WinRT 讀不到格式時仍要以 Win32 檢查隱私標記，避免記錄來源程式要求排除的內容
            ClipboardSkipReason win32Reason = await CheckWin32PrivacyAsync(cancellationToken).ConfigureAwait(true);
            if (win32Reason != ClipboardSkipReason.None)
            {
                _logger.LogInformation("略過剪貼簿內容（{Reason}，序號 {Sequence}）。", win32Reason, sequence);
                return;
            }
        }

        bool? hasText = TryContains(content, StandardDataFormats.Text, sequence);
        if (hasText is null)
        {
            await CaptureTextFallbackAsync(sequence, "無法判斷文字格式", cancellationToken).ConfigureAwait(true);
            return;
        }

        if (hasText == true)
        {
            await CaptureTextAsync(content, sequence, cancellationToken).ConfigureAwait(true);
            return;
        }

        bool? hasBitmap = TryContains(content, StandardDataFormats.Bitmap, sequence);
        if (hasBitmap is null)
        {
            await CaptureTextFallbackAsync(sequence, "無法判斷圖片格式", cancellationToken).ConfigureAwait(true);
            return;
        }

        if (hasBitmap == true)
        {
            await CaptureImageAsync(content, sequence, cancellationToken).ConfigureAwait(true);
            return;
        }

        // WinRT 的 AvailableFormats 偶爾在更新當下回傳空清單（來源程式尚未完成寫入），
        // 此時再試一次 Win32 文字讀取，避免把正常的文字剪貼誤判成不支援的格式。
        bool capturedByFallback = await CaptureTextFallbackAsync(sequence, "沒有支援的 WinRT 格式", cancellationToken)
            .ConfigureAwait(true);
        if (capturedByFallback)
        {
            return;
        }

        _logger.LogInformation(
            "略過不支援的剪貼簿格式（序號 {Sequence}）：{Formats}",
            sequence,
            DescribeFormats(content));
    }

    private async Task CaptureTextAsync(
        DataPackageView content,
        uint sequence,
        CancellationToken cancellationToken)
    {
        string? text = await ClipboardRetry.RunAsync(
            async () => await content.GetTextAsync(),
            cancellationToken: cancellationToken).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        AddText(text, sequence);
    }

    private async Task<bool> CaptureTextFallbackAsync(
        uint sequence,
        string reason,
        CancellationToken cancellationToken)
    {
        // WinRT 看不到格式時仍要尊重來源程式的隱私標記，因此先以 Win32 檢查再讀文字
        ClipboardSkipReason win32Reason = await CheckWin32PrivacyAsync(cancellationToken).ConfigureAwait(true);
        if (win32Reason != ClipboardSkipReason.None)
        {
            _logger.LogInformation("略過剪貼簿內容（{Reason}，序號 {Sequence}）。", win32Reason, sequence);
            return true;
        }

        IntPtr ownerWindow = _window.Handle;
        string? text = await Task.Run(
            () => ClipboardFormatReader.TryReadUnicodeText(ownerWindow),
            cancellationToken).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Win32 後援讀不到文字（{Reason}，序號 {Sequence}）。", reason, sequence);
            return false;
        }

        _logger.LogInformation("以 Win32 後援取得文字（{Reason}，序號 {Sequence}）。", reason, sequence);
        AddText(text, sequence);
        return true;
    }

    private void AddText(string? text, uint sequence)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("略過空白文字（序號 {Sequence}）。", sequence);
            return;
        }

        if (text.Length > MaxTextChars)
        {
            _logger.LogWarning("略過過大的文字剪貼簿內容（{Length} 字元，序號 {Sequence}）。", text.Length, sequence);
            return;
        }

        _history.AddText(text);
        _logger.LogInformation("已加入文字歷史（序號 {Sequence}，{Length} 字元）。", sequence, text.Length);
    }

    private async Task CaptureImageAsync(
        DataPackageView content,
        uint sequence,
        CancellationToken cancellationToken)
    {
        RandomAccessStreamReference? streamRef = await ClipboardRetry.RunAsync(
            async () => await content.GetBitmapAsync(),
            cancellationToken: cancellationToken).ConfigureAwait(true);
        if (streamRef is null)
        {
            _logger.LogDebug("剪貼簿圖片為空（序號 {Sequence}）。", sequence);
            return;
        }

        byte[] raw;
        using (IRandomAccessStreamWithContentType source = await streamRef.OpenReadAsync())
        {
            raw = await ReadAllBytesAsync(source).ConfigureAwait(true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        (byte[] png, byte[] thumbnail, byte[] hash) = await Task.Run(
            () => ProcessImageAsync(raw, cancellationToken),
            cancellationToken).ConfigureAwait(true);

        if (_disposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _history.AddImage(png, thumbnail, hash);
        _logger.LogInformation("已加入圖片歷史（序號 {Sequence}，{Bytes} bytes）。", sequence, png.Length);
    }

    private bool? TryContains(DataPackageView content, string format, uint sequence)
    {
        try
        {
            return content.Contains(format);
        }
        catch (Exception ex) when (IsReadUnavailable(ex))
        {
            _logger.LogDebug(ex, "無法判斷剪貼簿格式 {Format}（序號 {Sequence}）。", format, sequence);
            return null;
        }
    }

    private string DescribeFormats(DataPackageView content)
    {
        try
        {
            const int maxFormats = 8;
            string[] formats = content.AvailableFormats.ToArray();
            string summary = string.Join(", ", formats.Take(maxFormats));
            return formats.Length > maxFormats ? summary + " …" : summary;
        }
        catch (Exception ex) when (IsReadUnavailable(ex))
        {
            _logger.LogDebug(ex, "無法列出剪貼簿格式。");
            return "(無法讀取)";
        }
    }

    private async Task<(ClipboardSkipReason Reason, bool FormatsKnown)> EvaluatePrivacyAsync(
        DataPackageView content,
        CancellationToken cancellationToken)
    {
        string[] formats;
        try
        {
            formats = content.AvailableFormats.ToArray();
        }
        catch (Exception ex) when (IsReadUnavailable(ex))
        {
            _logger.LogDebug(ex, "無法取得剪貼簿格式清單，改以 Win32 檢查隱私標記。");
            return (ClipboardSkipReason.None, FormatsKnown: false);
        }

        if (ClipboardFormatReader.HasFormat(formats, ClipboardFormatReader.ExcludeMarkerFormat))
        {
            return (ClipboardSkipReason.ExcludedBySource, FormatsKnown: true);
        }

        if (!ClipboardFormatReader.HasFormat(formats, ClipboardFormatReader.CanIncludeInHistoryFormat))
        {
            return (ClipboardSkipReason.None, FormatsKnown: true);
        }

        // Win32 讀取可能短暫等待剪貼簿解鎖，放到背景執行緒避免卡住 UI
        IntPtr ownerWindow = _window.Handle;
        bool? canIncludeInHistory = await Task.Run(
            () => ClipboardFormatReader.TryReadDword(ownerWindow, ClipboardFormatReader.CanIncludeInHistoryFormat),
            cancellationToken).ConfigureAwait(true);

        return (ClipboardPrivacyPolicy.Evaluate(hasExcludeMarker: false, canIncludeInHistory), FormatsKnown: true);
    }

    private async Task<ClipboardSkipReason> CheckWin32PrivacyAsync(CancellationToken cancellationToken)
    {
        IntPtr ownerWindow = _window.Handle;
        return await Task.Run(
            () =>
            {
                bool hasExcludeMarker = ClipboardFormatReader.TryReadDword(
                    ownerWindow,
                    ClipboardFormatReader.ExcludeMarkerFormat) == true;
                bool? canIncludeInHistory = ClipboardFormatReader.TryReadDword(
                    ownerWindow,
                    ClipboardFormatReader.CanIncludeInHistoryFormat);
                return ClipboardPrivacyPolicy.Evaluate(hasExcludeMarker, canIncludeInHistory);
            },
            cancellationToken).ConfigureAwait(true);
    }

    private static bool IsReadUnavailable(Exception exception) =>
        ClipboardRetry.IsClipboardBusy(exception)
        || exception is InvalidOperationException or UnauthorizedAccessException;

    private static async Task<(byte[] Png, byte[] Thumbnail, byte[] Hash)> ProcessImageAsync(
        byte[] raw,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using InMemoryRandomAccessStream input = await CreateStreamAsync(raw);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
        using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        cancellationToken.ThrowIfCancellationRequested();
        byte[] png = await EncodePngAsync(softwareBitmap);
        // 直接由原始資料縮放解碼，避免再把剛編碼好的 PNG 整張解一次
        cancellationToken.ThrowIfCancellationRequested();
        byte[] thumbnail = await CreateThumbnailPngAsync(raw, softwareBitmap.PixelWidth, softwareBitmap.PixelHeight);
        byte[] hash = ClipboardHistoryService.ComputePixelHash(png);
        return (png, thumbnail, hash);
    }

    private static async Task<InMemoryRandomAccessStream> CreateStreamAsync(byte[] bytes)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return stream;
    }

    private static async Task<byte[]> EncodePngAsync(SoftwareBitmap bitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        return await ReadAllBytesAsync(stream);
    }

    private static async Task<byte[]> CreateThumbnailPngAsync(byte[] sourceBytes, int width, int height)
    {
        double scale = Math.Min(1.0, (double)ThumbnailWidth / Math.Max(1, width));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, (int)(width * scale)),
            ScaledHeight = (uint)Math.Max(1, (int)(height * scale)),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        using InMemoryRandomAccessStream input = await CreateStreamAsync(sourceBytes);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
        using SoftwareBitmap thumbnail = await decoder.GetSoftwareBitmapAsync(
            decoder.BitmapPixelFormat,
            decoder.BitmapAlphaMode,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);

        using var output = new InMemoryRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetSoftwareBitmap(thumbnail);
        await encoder.FlushAsync();
        return await ReadAllBytesAsync(output);
    }

    private static async Task<byte[]> ReadAllBytesAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        byte[] bytes = new byte[stream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
