using System.ComponentModel;
using System.Runtime.CompilerServices;
using Com.VibeCoding.SaveClipboard.Helpers;

namespace Com.VibeCoding.SaveClipboard.Models;

public sealed class ClipboardEntry : INotifyPropertyChanged
{
    private bool _isPinned;
    private bool _isSelected;
    private DateTimeOffset _copiedAt;
    private string? _preview;

    public required Guid Id { get; init; }

    public required ClipboardEntryKind Kind { get; init; }

    public string? Text { get; init; }

    public byte[]? ImageBytes { get; init; }

    public byte[]? ThumbnailBytes { get; init; }

    public byte[]? ImagePixelHash { get; init; }

    public string Key { get; init; } = string.Empty;

    public DateTimeOffset CopiedAt
    {
        get => _copiedAt;
        set
        {
            if (_copiedAt == value)
            {
                return;
            }

            _copiedAt = value;
            OnPropertyChanged();
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value)
            {
                return;
            }

            _isPinned = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string Preview
    {
        get
        {
            if (_preview is not null)
            {
                return _preview;
            }

            if (Kind == ClipboardEntryKind.Image)
            {
                return _preview = AppStrings.Get("Preview_Image");
            }

            string text = Text ?? string.Empty;
            string singleLine = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            const int max = 160;
            return _preview = singleLine.Length <= max ? singleLine : singleLine[..max] + "…";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
