namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 共用的剪貼簿記錄暫停狀態。托盤選單切換、監聽端查詢、面板顯示提示都讀這一份。
/// 暫停有期限（預設 30 分鐘），到期後自動恢復記錄。
/// </summary>
public sealed class ClipboardRecordingState
{
    public static readonly TimeSpan DefaultPauseDuration = TimeSpan.FromMinutes(30);

    private readonly Func<DateTimeOffset> _now;
    private DateTimeOffset? _pausedUntil;

    public ClipboardRecordingState(Func<DateTimeOffset>? now = null)
    {
        _now = now ?? (static () => DateTimeOffset.Now);
    }

    public bool IsPaused => _pausedUntil is { } until && _now() < until;

    public DateTimeOffset? PausedUntil => IsPaused ? _pausedUntil : null;

    public event Action? StateChanged;

    public void Pause(TimeSpan duration)
    {
        _pausedUntil = _now() + duration;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (_pausedUntil is null)
        {
            return;
        }

        _pausedUntil = null;
        StateChanged?.Invoke();
    }

    public void Toggle(TimeSpan duration)
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause(duration);
        }
    }
}
