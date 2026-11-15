namespace Com.VibeCoding.SaveClipboard.Helpers;

/// <summary>
/// 1x1 隱藏工具視窗，接收剪貼簿與系統列回呼訊息。
/// 預設保持隱藏，避免成為 shell 的「最後作用視窗」而被工作列啟用；
/// 顯示右鍵選單前才暫時顯示以取得前景，關閉後立即隱藏。
/// </summary>
public sealed class HelperWindow : IDisposable
{
    public const string ClassName = "SaveClipboardHelperWindow";
    public const string WindowTitle = "SaveClipboardHelper";

    private static readonly NativeMethods.WndProcDelegate StaticWndProc = WndProc;
    private static readonly Dictionary<IntPtr, HelperWindow> Instances = new();
    private static readonly object Sync = new();

    private IntPtr _hwnd;
    private bool _disposed;

    public HelperWindow()
    {
        IntPtr hInstance = NativeMethods.GetModuleHandleW(IntPtr.Zero);

        var wndClass = new NativeMethods.WNDCLASSEXW
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WNDCLASSEXW>(),
            lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(StaticWndProc),
            hInstance = hInstance,
            lpszClassName = ClassName,
        };

        NativeMethods.RegisterClassExW(wndClass);

        _hwnd = NativeMethods.CreateWindowExW(
            NativeMethods.WS_EX_TOOLWINDOW,
            ClassName,
            WindowTitle,
            NativeMethods.WS_POPUP,
            -32000,
            -32000,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("無法建立輔助視窗。");
        }

        lock (Sync)
        {
            Instances[_hwnd] = this;
        }
    }

    public IntPtr Handle => _hwnd;

    public event Action<uint, IntPtr, IntPtr>? MessageReceived;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (Sync)
        {
            Instances.Remove(_hwnd);
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        HelperWindow? instance;
        lock (Sync)
        {
            Instances.TryGetValue(hWnd, out instance);
        }

        if (instance is not null)
        {
            try
            {
                instance.MessageReceived?.Invoke(msg, wParam, lParam);
            }
            catch (Exception ex)
            {
                // 訊息回呼不可讓例外穿越原生邊界，但必須留下診斷線索
                DiagnosticLog.Write(nameof(HelperWindow), ex);
            }
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }
}
