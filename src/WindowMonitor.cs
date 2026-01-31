using ClunkyBorders.Common;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders;

internal class WindowMonitor : IDisposable
{
    public event EventHandler<Window?>? WindowChanged;

    private bool isStarted;
    private HWINEVENTHOOK eventHook;

    private bool disposed = false;

    public void Start()
    {
        try
        {
            if (isStarted)
            {
                Logger.Debug("WindowMonitor. Already started.");
                return;
            }

            Logger.Debug("WindowMonitor. Starting.");

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
                Logger.Error($"WindowMonitor. Failed to set SetWinEventHook. Error code: {Marshal.GetLastWin32Error()}");
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
            Logger.Error($"WindowMonitor. Error starting.", ex);
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
            Logger.Debug("WindowMonitor. Stopped.");
        }
        catch (Exception ex)
        {
            Logger.Error($"WindowMonitor. Error stopping.", ex);
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
                        // Only handle window-level location changes
                        if (idObject != 0) // 0 = OBJID_WINDOW
                            return;

                        // Only care about location changes for the active window
                        var activeHwnd = PInvoke.GetForegroundWindow();
                        if (hwnd != activeHwnd)
                            return;
                    }
                    break;

                case PInvoke.EVENT_SYSTEM_FOREGROUND:
                    break;

                case PInvoke.EVENT_OBJECT_DESTROY:
                case PInvoke.EVENT_OBJECT_HIDE:
                case PInvoke.EVENT_SYSTEM_MINIMIZESTART:
                    {
                        var activeHwnd = PInvoke.GetForegroundWindow();
                        if (hwnd != activeHwnd)
                            return;

                        // Only hide if the window is actually gone or hidden
                        // for whatever reasons active window that is not being closed may get those events 
                        if (!PInvoke.IsWindowVisible(hwnd) || !PInvoke.IsWindow(hwnd))
                        {
                            Logger.Debug($"WindowMonitor. Active window destruction/hide event detected: {hwnd}, event: {GetEventName(@event)}");
                            WindowChanged?.Invoke(this, null);
                        }
                        else
                        {
                            Logger.Debug($"WindowMonitor. Suppressed border hide: window still visible and valid.");
                        }
                        return;
                    }

                default:
                    return;
            }

            if (hwnd.IsNull)
                return;

            var window = GetWindow(hwnd);

            if (window != null && window.IsParent)
            {
                WindowChanged?.Invoke(this, window);
            }
            else
            {
                if (window != null && !window.IsParent)
                {
                    Logger.Debug($"WindowMonitor. Ignoring non-parent window: {window.ClassName} - {window.Text}");
                }
            }

        }
        catch (Exception ex)
        {
            Logger.Error($"WindowMonitor. Error in OnWindowChange.", ex);
        }
    }

    private Window? GetWindow(HWND hwnd)
    {
        try
        {
            // Validate window handle is still valid
            if (!PInvoke.IsWindow(hwnd))
            {
                Logger.Debug($"WindowMonitor. Window handle {hwnd} is no longer valid");
                return null;
            }

            var className = GetWindowClassName(hwnd);
            var text = GetWindowText(hwnd);
            var rect = GetWindowArea(hwnd);
            var state = GetWindowState(hwnd);
            var isParent = IsParentWindow(hwnd);
            uint dpi = PInvoke.GetDpiForWindow(hwnd);

            return new Window
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
            Logger.Error($"WindowMonitor. Error getting window {hwnd} information.", ex);
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

            Logger.Error($"WindowMonitor. Error getting extended window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");

            // fallback
            var result = PInvoke.GetWindowRect(hwnd, out rect);

            if (result == 0)
            {
                Logger.Error($"WindowMonitor. Error getting window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");
                return default;
            }

            return rect;
        }
        catch (Exception ex)
        {
            Logger.Error($"WindowMonitor. Error getting window ({hwnd}) rect.", ex);
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
            Logger.Error($"WindowMonitor. Error checking window parent.", ex);
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
            Logger.Error($"WindowMonitor. Error getting window state.");
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

    private Window? GetCurrentActiveWindow()
    {
        try
        {
            var hwnd = PInvoke.GetForegroundWindow();

            if (hwnd.IsNull)
            {
                Logger.Error("WindowMonitor. No active window.");
                return null;
            }

            return GetWindow(hwnd);

        }
        catch (Exception ex)
        {
            Logger.Error("WindowMonitor. Error getting current active window.", ex);
            return null;
        }
    }

    private string GetEventName(uint @event)
    {
        return @event switch
        {
            PInvoke.EVENT_OBJECT_DESTROY => "DESTROY",
            PInvoke.EVENT_OBJECT_HIDE => "HIDE",
            PInvoke.EVENT_SYSTEM_MINIMIZESTART => "MINIMIZESTART",
            _ => "UNKNOWN"
        };
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

    ~WindowMonitor()
    {
        Dispose(false);
    }

}

