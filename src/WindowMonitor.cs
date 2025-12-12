using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders
{
    internal class WindowMonitor
    {
        // todo: move to configuration
        HashSet<string> classNamesToExclude = new HashSet<string>()
        {
            "Windows.UI.Core.CoreWindow",               // Windows Start menu
            "Shell_TrayWnd",                            // Windows taskbar
            "TopLevelWindowForOverflowXamlIsland",      // Windows tray show hidden icons
            "XamlExplorerHostIslandWindow",             // Windows Task Swicher
            "ForegroundStaging",                        // Windows Task Swicher - temporary window
            "Progman"                                   // Program Manager - e.g when clicking a desktop
        };

        public event EventHandler<WindowInfo?>? WindowChanged;

        private bool isStarted;

        private HWINEVENTHOOK eventHook;

        // todo: add try catch
        public void Start()
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

            if(eventHook == IntPtr.Zero)
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


                // todo: right now we have to allow for null otherwise we do not hide border properly
                //  we could filter in Program class but then we don't want to do more stuff then necessary
                //  in GetWindow or filter in both places - think on it

                //if (window != null)
                {
                    WindowChanged?.Invoke(this, window);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowMonitor -> Error in OnWindowChange. Exception: {ex}");
            }
        }

        // todo: only partent windows - filter dialogs, splash screens etc.
        // todo: try catch if something is not right
        private WindowInfo? GetWindow(HWND hwnd)
        {
            // todo: check if hwnd is null

            // todo: try catch - sometimes we get FAIL 

            var windowClassName = GetWindowClassName(hwnd);

            if (classNamesToExclude.Contains(windowClassName))
            {
                Console.WriteLine($"WindowMonitor -> Excluded window detected: {windowClassName}");

                // window is excluded
                return null;
            }

            var windowText = GetWindowText(hwnd);

            // todo: fail if not received
            // todo: boundaries for some windows are off why?
            PInvoke.GetWindowRect(hwnd, out var rect);

            WindowState state = GetWindowState(hwnd);

            return new WindowInfo { Handle = hwnd, ClassName = windowClassName, Text = windowText, Rect = rect, State = state };
        }

        private static WindowState GetWindowState(HWND hwnd)
        {
            var placement = new WINDOWPLACEMENT();
            placement.length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>();

            PInvoke.GetWindowPlacement(hwnd, ref placement);

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
