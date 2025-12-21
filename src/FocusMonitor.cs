using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders
{
    internal class FocusMonitor : IDisposable
    {
        public event EventHandler<WindowInfo?>? WindowChanged;

        private bool isStarted;
        private HWINEVENTHOOK eventHook;
        private readonly Logger logger;

        private bool disposed = false;

        public FocusMonitor(Logger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start()
        {
            try
            {
                if (isStarted)
                {
                    logger.Debug("FocusMonitor. Already started.");
                    return;
                }

                logger.Debug("FocusMonitor. Starting.");

                eventHook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                    HMODULE.Null,                           // In process hook
                    OnWindowChange,                         // In process callback function
                    0,                                      // All processes
                    0,                                      // All threads
                    PInvoke.WINEVENT_OUTOFCONTEXT           // In process hook
                    | PInvoke.WINEVENT_SKIPOWNPROCESS
                );

                if (eventHook == IntPtr.Zero)
                {
                    logger.Error($"FocusMonitor. Failed to set SetWinEventHook. Error code: {Marshal.GetLastWin32Error()}");
                    return;
                }

                var window = GetCurrentActiveWindow();
                if (window != null)
                {
                    WindowChanged?.Invoke(this, window);
                }

                isStarted = true;
            }
            catch(Exception ex) 
            {
                logger.Error($"FocusMonitor. Error starting.", ex);
            }
        }

        public void Stop()
        {
            if (!isStarted)
                return;

            try
            {
                if(eventHook != IntPtr.Zero)
                {
                    PInvoke.UnhookWinEvent(eventHook);
                    eventHook = default;
                }

                isStarted = false;
                logger.Debug("FocusMonitor. Stopped.");
            }
            catch (Exception ex)
            {
                logger.Error($"FocusMonitor. Error stopping.", ex);
            }
        }

        private void OnWindowChange(HWINEVENTHOOK hWinEventHook, uint @event, HWND hwnd,
            int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
        {
            try
            {
                switch (@event)
                {
                    case PInvoke.EVENT_OBJECT_LOCATIONCHANGE:
                        {
                            // Only care about location changes for the active window
                            var activeHwnd = PInvoke.GetForegroundWindow();
                            if (hwnd != activeHwnd)
                                return;
                        }
                        break;

                    case PInvoke.EVENT_SYSTEM_FOREGROUND:
                        break;

                    default:
                        return;
                }

                if (hwnd.IsNull)
                    return;

                var window = GetWindow(hwnd);

                if (window != null)
                {
                    WindowChanged?.Invoke(this, window);
                }

            }
            catch (Exception ex)
            {
                logger.Error($"FocusMonitor. Error in OnWindowChange.", ex);
            }
        }

        private WindowInfo? GetWindow(HWND hwnd)
        {
            try
            {
                var className = GetWindowClassName(hwnd);
                var text = GetWindowText(hwnd);
                var rect = GetWindowArea(hwnd);
                var state = GetWindowState(hwnd);
                var isParent = IsParentWindow(hwnd);
                uint dpi = PInvoke.GetDpiForWindow(hwnd);

                return new WindowInfo
                {
                    Handle = hwnd,
                    ClassName = className,
                    Text = text,
                    Rect = rect,
                    State = state,
                    IsParent = isParent,
                    DPI = dpi
                };
            }
            catch (Exception ex)
            {
                logger.Error($"FocusMonitor. Error getting window {hwnd} information.", ex);
                return null;
            }
        }

        private unsafe RECT GetWindowArea(HWND hwnd)
        {
            try
            {
                RECT rect;

                var hResult = PInvoke.DwmGetWindowAttribute(
                    hwnd,
                    DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                    &rect,    
                    (uint)sizeof(RECT)
                );

                if (hResult.Succeeded)
                    return rect;

                logger.Error($"FocusMonitor. Error getting extended window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");

                // fallback
                var result = PInvoke.GetWindowRect(hwnd, out rect);

                if (result == 0)
                {
                    logger.Error($"FocusMonitor. Error getting window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");
                    return default;
                }

                return rect;
            }
            catch (Exception ex)
            {
                logger.Error($"FocusMonitor. Error getting window ({hwnd}) rect.", ex);
                return default;
            }
        }

        private bool IsParentWindow(HWND hwnd)
        {
            try
            {
                var rootWindow = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
                if(rootWindow.IsNull) 
                    return false;

                // if different then selected window is not a parent
                if (rootWindow != hwnd)
                    return false;

                var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                if ((style & (uint)WINDOW_STYLE.WS_POPUP) != 0 && (style & (uint)WINDOW_STYLE.WS_CAPTION) == 0)
                    return false;

                var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                if ((exStyle & (uint)WINDOW_EX_STYLE.WS_EX_DLGMODALFRAME) != 0)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"FocusMonitor. Error checking window parent.", ex);
                return false;
            }
        }

        private WindowState GetWindowState(HWND hwnd)
        {
            var placement = new WINDOWPLACEMENT();
            placement.length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>();

            var result = PInvoke.GetWindowPlacement(hwnd, ref placement);

            if (result == 0)
            {
                logger.Error($"FocusMonitor. Error getting window state.");
                return WindowState.Unknown;
            }

            return (uint)placement.showCmd switch
            {
                0 => WindowState.Hiden,
                1 => WindowState.Normal,
                2 => WindowState.Minimized,
                3 => WindowState.Maximized,
                _ => WindowState.Unknown,
            };
        }

        private unsafe string GetWindowClassName(HWND hwnd)
        {
            const int maxLength = 256;
            var buffer = new char[maxLength];

            fixed (char* pBuffer = buffer)
            {
                var length = PInvoke.GetClassName(hwnd, pBuffer, maxLength);
                return length == 0 ? string.Empty : new string(pBuffer, 0, length);
            }
        }

        private unsafe string GetWindowText(HWND hwnd)
        {
            const int maxLength = 256;
            var buffer = new char[maxLength];

            fixed (char* pBuffer = buffer)
            {
                var length = PInvoke.GetWindowText(hwnd, pBuffer, maxLength);
                return length == 0 ? string.Empty : new string(pBuffer, 0, length);
            }
        }

        private WindowInfo? GetCurrentActiveWindow()
        {
            try
            {
                var hwnd = PInvoke.GetForegroundWindow();

                if (hwnd.IsNull)
                {
                    logger.Error("FocusMonitor. No active window.");
                    return null;
                }

                return GetWindow(hwnd);

            }
            catch (Exception ex)
            {
                logger.Error($"FocusMonitor. Error getting current active window.", ex);
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            Stop();

            disposed = true;
        }

        ~FocusMonitor()
        {
            Dispose(false);
        }

    }
}

