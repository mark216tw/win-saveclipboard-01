using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Com.VibeCoding.SaveClipboard.Services;

/// <summary>
/// 面板鍵盤導覽：搜尋框與清單焦點移動、Enter 複製、Delete 刪除、Esc 清除搜尋或關閉。
/// 視窗只保留事件轉接，讓 UI 邏輯可以被單獨檢視。
/// </summary>
public sealed class KeyboardNavigationService
{
    private readonly HistoryPanelViewModel _viewModel;
    private readonly ListView _entryList;
    private readonly TextBox _searchBox;
    private readonly FrameworkElement _root;
    private readonly Func<bool> _isInputBlocked;
    private readonly Func<bool> _isSettingsOpen;
    private readonly Action _resetSearch;
    private readonly Action _hidePanel;
    private readonly Func<Task> _deleteAsync;

    public KeyboardNavigationService(
        HistoryPanelViewModel viewModel,
        ListView entryList,
        TextBox searchBox,
        FrameworkElement root,
        Func<bool> isInputBlocked,
        Func<bool> isSettingsOpen,
        Action resetSearch,
        Action hidePanel,
        Func<Task> deleteAsync)
    {
        _viewModel = viewModel;
        _entryList = entryList;
        _searchBox = searchBox;
        _root = root;
        _isInputBlocked = isInputBlocked;
        _isSettingsOpen = isSettingsOpen;
        _resetSearch = resetSearch;
        _hidePanel = hidePanel;
        _deleteAsync = deleteAsync;
    }

    public async Task HandleKeyDownAsync(KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (!_isInputBlocked() && !string.IsNullOrEmpty(_searchBox.Text))
            {
                // 有搜尋條件時，第一次 Esc 先清除搜尋，再按一次才關閉面板
                _resetSearch();
                _searchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (_isSettingsOpen())
            {
                _viewModel.Settings.CloseCommand.Execute(null);
            }
            else if (_viewModel.AutoHideEnabled)
            {
                _hidePanel();
            }

            e.Handled = true;
            return;
        }

        if (_isInputBlocked())
        {
            return;
        }

        bool searchFocused = IsSearchBoxFocused();

        if (e.Key == Windows.System.VirtualKey.F && IsControlDown())
        {
            e.Handled = true;
            _searchBox.Focus(FocusState.Programmatic);
            return;
        }

        if (e.Key == Windows.System.VirtualKey.A && !searchFocused && IsControlDown())
        {
            e.Handled = true;
            SelectAll();
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down:
                if (_viewModel.VisibleItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                MoveFocusIntoList(1);
                return;

            case Windows.System.VirtualKey.Up:
                if (_viewModel.VisibleItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                MoveFocusIntoList(-1);
                return;

            case Windows.System.VirtualKey.Home:
                if (_viewModel.VisibleItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                FocusItem(0);
                return;

            case Windows.System.VirtualKey.End:
                if (_viewModel.VisibleItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                FocusItem(_viewModel.VisibleItems.Count - 1);
                return;

            case Windows.System.VirtualKey.PageDown:
                if (_viewModel.VisibleItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                MoveFocusIntoList(5);
                return;

            case Windows.System.VirtualKey.PageUp:
                if (_viewModel.VisibleItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                MoveFocusIntoList(-5);
                return;

            case Windows.System.VirtualKey.Enter:
                ClipboardEntry? target = GetFocusedEntry()
                    ?? (searchFocused ? _viewModel.VisibleItems.FirstOrDefault() : null);
                if (target is not null)
                {
                    e.Handled = true;
                    await _viewModel.CopyCommand.ExecuteAsync(target);
                }

                return;

            case Windows.System.VirtualKey.Delete:
                if (searchFocused)
                {
                    // 搜尋框內的 Delete 交給 TextBox 處理
                    return;
                }

                e.Handled = true;
                await _deleteAsync();
                return;
        }
    }

    public void SelectAll()
    {
        foreach (ClipboardEntry entry in _viewModel.VisibleItems)
        {
            entry.IsSelected = true;
        }
    }

    public static bool IsControlDown() => IsKeyDown(Windows.System.VirtualKey.Control);

    public static bool IsShiftDown() => IsKeyDown(Windows.System.VirtualKey.Shift);

    private static bool IsKeyDown(Windows.System.VirtualKey key)
    {
        Windows.UI.Core.CoreVirtualKeyStates state =
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private bool IsSearchBoxFocused()
    {
        XamlRoot? xamlRoot = _root.XamlRoot;
        return xamlRoot is not null
            && ReferenceEquals(FocusManager.GetFocusedElement(xamlRoot), _searchBox);
    }

    private void MoveFocusIntoList(int delta)
    {
        System.Collections.ObjectModel.ObservableCollection<ClipboardEntry> items = _viewModel.VisibleItems;
        if (items.Count == 0)
        {
            return;
        }

        int current = GetFocusedEntry() is { } focused ? items.IndexOf(focused) : -1;
        int next = current < 0
            ? (delta >= 0 ? 0 : items.Count - 1)
            : Math.Clamp(current + delta, 0, items.Count - 1);

        FocusItem(next);
    }

    private void FocusItem(int index)
    {
        ClipboardEntry entry = _viewModel.VisibleItems[index];
        _entryList.ScrollIntoView(entry);
        _entryList.UpdateLayout();

        if (_entryList.ContainerFromIndex(index) is ListViewItem container)
        {
            container.Focus(FocusState.Programmatic);
            return;
        }

        // 虛擬化尚未產生容器時，等下一輪配置完成再聚焦一次
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            if (_entryList.ContainerFromIndex(index) is ListViewItem delayed)
            {
                delayed.Focus(FocusState.Programmatic);
            }
        });
    }

    public ClipboardEntry? GetFocusedEntry()
    {
        XamlRoot? xamlRoot = _root.XamlRoot;
        if (xamlRoot is null)
        {
            return null;
        }

        DependencyObject? current = FocusManager.GetFocusedElement(xamlRoot) as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: ClipboardEntry entry })
            {
                return entry;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
