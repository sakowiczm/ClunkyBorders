using ClunkyBorders.Common;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

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

            var window = Window.GetForeground();
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
            // Early return for non-foreground windows (except for foreground change events)
            if (@event != PInvoke.EVENT_SYSTEM_FOREGROUND && !Window.IsForeground(hwnd))
                return;

            switch (@event)
            {
                case PInvoke.EVENT_OBJECT_LOCATIONCHANGE:
                    {
                        // Only handle window-level location changes
                        if (idObject != 0) // 0 = OBJID_WINDOW
                            return;
                    }
                    break;

                case PInvoke.EVENT_SYSTEM_FOREGROUND:
                    break;

                case PInvoke.EVENT_OBJECT_DESTROY:
                case PInvoke.EVENT_OBJECT_HIDE:
                case PInvoke.EVENT_SYSTEM_MINIMIZESTART:
                    {
                        // Only hide if the window is actually gone or hidden
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

            var window = Window.FromHandle(hwnd);

            if (window != null && window.IsParent)
            {
                WindowChanged?.Invoke(this, window);
            }
            else
            {
                if (window != null)
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

