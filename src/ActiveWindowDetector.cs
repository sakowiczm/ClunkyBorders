using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace ClunkyBorders
{
    internal class ActiveWindowDetector
    {
        // todo: move to configuration
        HashSet<string> classNamesToExclude = new HashSet<string>()
        {
            "Windows.UI.Core.CoreWindow",               // Windows Start menu
            "Shell_TrayWnd",                            // Windows taskbar
            "TopLevelWindowForOverflowXamlIsland",      // Windows tray show hidden icons
            "XamlExplorerHostIslandWindow",             // Windows Task Swicher
            "ForegroundStaging",                        // Windows Task Swicher - temporary window
        };

        public event EventHandler<Window?>? WindowChanged;

        private bool _isStarted;

        // todo: add try catch
        public void Start()
        {
            if (_isStarted)
            {
                Console.WriteLine("ActiveWindowDetector is already started.");
                return;
            }

            Console.WriteLine("ActiveWindowDetector is starting...");


            // todo: what if this one is null?
            // todo: do I need handle somehow on Stop?
            var hookHwnd = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                HMODULE.Null,
                OnWindowChange,
                0,
                0,
                0x0000u | 0x0002u
            );

            var window = GetCurrentActiveWindow();
            if (window != null)
            {
                WindowChanged?.Invoke(this, window);
            }

            _isStarted = true;
        }

        // todo: add try catch
        public void Stop()
        {
            if (!_isStarted)
                return;

            Console.WriteLine("ActiveWindowDetector is stopping...");

            _isStarted = false;
        }

        private void OnWindowChange(
                HWINEVENTHOOK hWinEventHook,
                uint @event,
                HWND hwnd,
                int idObject,
                int idChild,
                uint idEventThread,
                uint dwmsEventTime)
        {
            try
            {
                // todo: handle resize as well
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
        private Window? GetWindow(HWND hwnd)
        {
            var windowClassName = GetWindowClassName(hwnd);

            if (classNamesToExclude.Contains(windowClassName))
            {
                // window is excluded
                return null;
            }

            var windowText = GetWindowText(hwnd);

            return new Window { Handle = hwnd, ClassName = windowClassName, Text = windowText };
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
