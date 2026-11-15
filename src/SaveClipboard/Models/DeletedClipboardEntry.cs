namespace Com.VibeCoding.SaveClipboard.Models;

/// <summary>被刪除的歷史項目與原始索引，用於復原。</summary>
public readonly record struct DeletedClipboardEntry(ClipboardEntry Entry, int Index);
