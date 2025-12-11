using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

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

        // todo: add try catch
        public void Start()
        {
            if (isStarted)
            {
                Console.WriteLine("WindowMonitor is already started.");
                return;
            }

            Console.WriteLine("WindowMonitor is starting...");

            // todo: what if this one is null?
            // todo: do I need handle somehow on Stop?
            var hookHwnd = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,        // Event Min
                PInvoke.EVENT_SYSTEM_FOREGROUND,        // Event Max
                HMODULE.Null,                           // In process hook
                OnWindowChange,                         // In process callback function
                0,                                      // All processes
                0,                                      // All threads
                PInvoke.WINEVENT_OUTOFCONTEXT           // In process hook
                | PInvoke.WINEVENT_SKIPOWNPROCESS
            );

            var window = GetCurrentActiveWindow();
            if (window != null)
            {
                WindowChanged?.Invoke(this, window);
            }

            isStarted = true;
        }

        // todo: add try catch
        public void Stop()
        {
            if (!isStarted)
                return;

            Console.WriteLine("WindowMonitor is stopping...");

            isStarted = false;
        }

        private void OnWindowChange(
            HWINEVENTHOOK hWinEventHook, // Handle to the event hook
            uint @event,                 // Event type   
            HWND hwnd,                   // Window that triggered the event
            int idObject, int idChild,   
            uint idEventThread,          // Thread that triggered the event
            uint dwmsEventTime)          // Timestamp of the event
        {
            try
            {
                // todo: handle resize as well
                // todo: handle move
                if (@event != PInvoke.EVENT_SYSTEM_FOREGROUND)
                    return;

                var window = GetWindow(hwnd);

                WindowChanged?.Invoke(this, window);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }

        // todo: only partent windows - filter dialogs, splash screens etc.
        // todo: try catch if something is not right
        private WindowInfo? GetWindow(HWND hwnd)
        {
            // todoL check if hwnd is null

            var windowClassName = GetWindowClassName(hwnd);

            if (classNamesToExclude.Contains(windowClassName))
            {
                // window is excluded
                return null;
            }

            var windowText = GetWindowText(hwnd);

            // todo: fail if not received
            PInvoke.GetWindowRect(hwnd, out var rect);

            return new WindowInfo { Handle = hwnd, ClassName = windowClassName, Text = windowText, Rect = rect };
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
                    Console.WriteLine("No active window.");
                    return null;
                }

                return GetWindow(hwnd);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current active window. Error: {ex}");
                return null;
            }
        }

    }
}
