using System.Text;
using System.Threading.Channels;
using Com.VibeCoding.SaveClipboard.Helpers;
using Microsoft.Extensions.Logging;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 簡易檔案日誌（%LOCALAPPDATA%\SaveClipboard\Logs\app.log），用於診斷常駐程式問題。
/// 以有界佇列在背景批次寫入，超過上限時輪替為 app.log.1，避免阻塞 UI 與無限成長。
/// </summary>
public sealed class DiagnosticsFileLoggerProvider : ILoggerProvider
{
    private const long MaxBytes = 1024 * 1024;

    private readonly object _gate = new();
    private readonly Channel<string> _channel;
    private readonly string _path = Path.Combine(DiagnosticLog.LogDirectory, "app.log");
    private readonly Task _writerTask;
    private long _currentLength;

    public DiagnosticsFileLoggerProvider()
    {
        try
        {
            Directory.CreateDirectory(DiagnosticLog.LogDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 無法建立日誌目錄時，寫入階段會失敗並被忽略
        }

        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _currentLength = GetCurrentLength();
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // 結束時未能排空日誌不影響程式關閉
        }
    }

    private long GetCurrentLength()
    {
        try
        {
            var info = new FileInfo(_path);
            return info.Exists ? info.Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private async Task WriteLoopAsync()
    {
        var buffer = new StringBuilder();
        await foreach (string line in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            buffer.Clear();
            buffer.AppendLine(line);
            while (_channel.Reader.TryRead(out string? next))
            {
                buffer.AppendLine(next);
                if (buffer.Length >= 64 * 1024)
                {
                    break;
                }
            }

            WriteBatch(buffer);
        }
    }

    private void WriteBatch(StringBuilder buffer)
    {
        string text = buffer.ToString();
        lock (_gate)
        {
            try
            {
                if (_currentLength + text.Length > MaxBytes)
                {
                    try
                    {
                        File.Move(_path, _path + ".1", overwrite: true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        File.WriteAllText(_path, string.Empty);
                    }

                    _currentLength = 0;
                }

                File.AppendAllText(_path, text);
                _currentLength += text.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 無法寫入日誌時不中斷程式
            }
        }
    }

    private void Enqueue(string line) => _channel.Writer.TryWrite(line);

    private sealed class FileLogger : ILogger
    {
        private readonly DiagnosticsFileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(DiagnosticsFileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            _provider.Enqueue(line);
        }
    }
}
