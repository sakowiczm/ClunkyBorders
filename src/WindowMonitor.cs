using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders
{
    internal class WindowMonitor
    {
        public event EventHandler<WindowInfo?>? WindowChanged;

        private bool isStarted;

        private HWINEVENTHOOK eventHook;

        public void Start()
        {
            try
            {
                if (isStarted)
                {
                    Console.WriteLine("WindowMonitor -> Is already started.");
                    return;
                }

                Console.WriteLine("WindowMonitor -> Starting...");

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
                    // todo: GetLastError?
                    Console.WriteLine("WindowMonitor -> Failed to set SetWinEventHook.");
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
                Console.WriteLine($"WindowMonitor -> Error starting. Exception: {ex}");
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
                Console.WriteLine("WindowMonitor -> Stopped.");
            }
            catch (Exception ex)
            {

                Console.WriteLine($"WindowMonitor -> Error stopping. Exception: {ex}");
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

                var window = GetWindow(hwnd);

                if (window != null)
                {
                    WindowChanged?.Invoke(this, window);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowMonitor -> Error in OnWindowChange. Exception: {ex}");
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

                return new WindowInfo 
                { 
                    Handle = hwnd, 
                    ClassName = className, 
                    Text = text, 
                    Rect = rect, 
                    State = state, 
                    IsParent = isParent 
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowMonitor -> Error getting window {hwnd} information. Exception: {ex}");
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

                Console.WriteLine("WindowMonitor -> Error getting extended window area.");

                // fallback
                var result = PInvoke.GetWindowRect(hwnd, out rect);

                if (result == 0)
                {
                    Console.WriteLine($"WindowMonitor -> Error getting window rect.");
                    return default;
                }

                return rect;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowMonitor -> Error getting window rect. Exception: {ex}");
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
                Console.WriteLine($"WindowMonitor -> Error checking window parent. Exception: {ex}");
                return false;
            }
        }

        private static WindowState GetWindowState(HWND hwnd)
        {
            var placement = new WINDOWPLACEMENT();
            placement.length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>();

            var result = PInvoke.GetWindowPlacement(hwnd, ref placement);

            if (result == 0)
            {
                Console.WriteLine($"WindowMonitor -> Error getting window state.");
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
                    Console.WriteLine("WindowMonitor -> No active window.");
                    return null;
                }

                return GetWindow(hwnd);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowMonitor -> Error getting current active window. Error: {ex}");
                return null;
            }
        }

    }
}
