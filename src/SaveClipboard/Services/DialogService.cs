using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 面板內的 ContentDialog 管理。WinUI 同時只允許一個對話框，
/// 這裡集中處理重複觸發、例外與面板自動隱藏保護。
/// </summary>
public sealed class DialogService
{
    private readonly FrameworkElement _root;
    private readonly Action<bool> _setAllowHide;
    private readonly ILogger _logger;
    private bool _dialogOpen;

    public DialogService(FrameworkElement root, Action<bool> setAllowHide, ILogger logger)
    {
        _root = root;
        _setAllowHide = setAllowHide;
        _logger = logger;
    }

    public bool IsDialogOpen => _dialogOpen;

    public Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText) =>
        ShowAsync(title, message, closeText, primaryText);

    public async Task<bool> ShowAsync(string title, string message, string closeText, string? primaryText = null)
    {
        if (_dialogOpen)
        {
            // 重複觸發時視為取消，避免 ContentDialog 例外中斷常駐程式
            return false;
        }

        _dialogOpen = true;
        _setAllowHide(false);
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = _root.XamlRoot,
                Title = title,
                Content = message,
                PrimaryButtonText = primaryText ?? string.Empty,
                CloseButtonText = closeText,
                DefaultButton = primaryText is null ? ContentDialogButton.Close : ContentDialogButton.Close,
            };

            if (primaryText is null)
            {
                dialog.PrimaryButtonText = string.Empty;
            }

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "顯示對話框失敗。");
            return false;
        }
        finally
        {
            _setAllowHide(true);
            _dialogOpen = false;
        }
    }
}
