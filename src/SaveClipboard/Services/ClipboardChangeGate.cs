namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 標記本程式剛設定剪貼簿，讓監聽端略過隨之而來的 WM_CLIPBOARDUPDATE，
/// 避免從歷史複製後又整筆重新解碼、編碼並加回清單。
/// 以寫入前後的剪貼簿序號範圍比對而非時間窗，避免使用者在短時間內真正複製的新內容被誤略過；
/// 同一筆自我寫入若產生多次更新通知，範圍內的序號都會被略過。
/// </summary>
public sealed class ClipboardChangeGate
{
    // 0 代表尚未設定期待值（GetClipboardSequenceNumber 失敗時也回 0）
    private long _startSequence;
    private long _endSequence;

    public void ExpectOwnChange(uint startSequence, uint endSequence)
    {
        Interlocked.Exchange(ref _startSequence, startSequence);
        Interlocked.Exchange(ref _endSequence, endSequence);
    }

    public void ExpectOwnChange(uint sequence) => ExpectOwnChange(sequence, sequence);

    public void Clear()
    {
        Interlocked.Exchange(ref _startSequence, 0);
        Interlocked.Exchange(ref _endSequence, 0);
    }

    public bool TryConsume(uint currentSequence)
    {
        if (currentSequence == 0)
        {
            return false;
        }

        uint start = (uint)Interlocked.Read(ref _startSequence);
        uint end = (uint)Interlocked.Read(ref _endSequence);
        if (end == 0)
        {
            return false;
        }

        return unchecked(currentSequence - start) <= unchecked(end - start);
    }
}
