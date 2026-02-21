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
    private HWINEVENTHOOK locationEventHook;
    private HWINEVENTHOOK stateEventHook;
    private WINEVENTPROC? locationEventDelegate;
    private WINEVENTPROC? stateEventDelegate;    

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

            // Store delegate reference to prevent garbage collection
            locationEventDelegate = OnWindowChange;
            stateEventDelegate = OnWindowChange;

            locationEventHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                HMODULE.Null,                           // In process hook
                locationEventDelegate,
                0,                                      // All processes
                0,                                      // All threads
                PInvoke.WINEVENT_OUTOFCONTEXT           // In process hook
                | PInvoke.WINEVENT_SKIPOWNPROCESS
            );

            if (locationEventHook == IntPtr.Zero)
            {
                Logger.Error($"WindowMonitor. Failed to set location event hook. Error code: {Marshal.GetLastWin32Error()}");
                return;
            }

            // Hook for state changes (maximize, minimize, fullscreen)
            stateEventHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_STATECHANGE,
                PInvoke.EVENT_OBJECT_STATECHANGE,
                HMODULE.Null,
                stateEventDelegate,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS
            );

            if (stateEventHook == IntPtr.Zero)
            {
                Logger.Error($"WindowMonitor. Failed to set state event hook. Error: {Marshal.GetLastWin32Error()}");

                // Cleanup location hook before returning
                PInvoke.UnhookWinEvent(locationEventHook);
                locationEventHook = default;
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
            // Set flag FIRST to prevent new callbacks from processing
            isStarted = false;

            if (locationEventHook != IntPtr.Zero)
            {
                PInvoke.UnhookWinEvent(locationEventHook);
                locationEventHook = default;
            }

            if (stateEventHook != IntPtr.Zero)
            {
                PInvoke.UnhookWinEvent(stateEventHook);
                stateEventHook = default;
            }

            // Wait briefly for any in-flight callbacks to complete
            // WINEVENT_OUTOFCONTEXT callbacks should drain quickly
            Thread.Sleep(50);

            // Now safe to clear delegates
            locationEventDelegate = null;
            stateEventDelegate = null;

            Logger.Debug("WindowMonitor. Stopped.");
        }
        catch (Exception ex)
        {
            Logger.Error("WindowMonitor. Error stopping.", ex);
        }
    }

    private void OnWindowChange(HWINEVENTHOOK hWinEventHook, uint @event, HWND hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        try
        {
            if (disposed || !isStarted)
            {
                Logger.Debug("WindowMonitor. Callback fired after disposal/stop. Ignoring.");
                return;
            }

            // Early return for non-foreground windows (except for foreground change events)
            if (@event != PInvoke.EVENT_SYSTEM_FOREGROUND && !Window.IsForeground(hwnd))
                return;

            switch (@event)
            {
                case PInvoke.EVENT_OBJECT_LOCATIONCHANGE:
                case PInvoke.EVENT_OBJECT_STATECHANGE:
                    {
                        // Only handle window-level changes changes, not child controls
                        if (idObject != 0) // 0 = OBJID_WINDOW
                            return;

                        // For location/state changes, always get the current foreground window
                        // instead of using the hwnd from the event, as apps like Windows Terminal
                        // may fire events for child windows that aren't the actual foreground window
                        var foregroundWindow = Window.GetForeground();
                        if (foregroundWindow != null)
                        {
                            Logger.Debug($"WindowMonitor. Processing {GetEventName(@event)} for foreground window: {foregroundWindow.ClassName}");
                            WindowChanged?.Invoke(this, foregroundWindow);
                        }
                        return;
                    }

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
            PInvoke.EVENT_SYSTEM_FOREGROUND => "FOREGROUND",
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE => "LOCATIONCHANGE",
            PInvoke.EVENT_OBJECT_STATECHANGE => "STATECHANGE",
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

