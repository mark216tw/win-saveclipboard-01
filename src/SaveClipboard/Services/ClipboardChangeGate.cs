namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 標記本程式剛設定剪貼簿，讓監聽端略過隨之而來的 WM_CLIPBOARDUPDATE，
/// 避免從歷史複製後又整筆重新解碼、編碼並加回清單。
/// 以剪貼簿序號比對而非時間窗，避免使用者在短時間內真正複製的新內容被誤略過。
/// </summary>
public sealed class ClipboardChangeGate
{
    // 0 代表尚未設定期待值（GetClipboardSequenceNumber 失敗時也回 0）
    private long _expectedSequence;

    public void ExpectOwnChange(uint sequence) => Interlocked.Exchange(ref _expectedSequence, sequence);

    public void Clear() => Interlocked.Exchange(ref _expectedSequence, 0);

    public bool TryConsume(uint currentSequence)
    {
        if (currentSequence == 0)
        {
            return false;
        }

        long expected = Interlocked.Read(ref _expectedSequence);
        if (expected == 0 || expected != currentSequence)
        {
            return false;
        }

        Interlocked.Exchange(ref _expectedSequence, 0);
        return true;
    }
}
