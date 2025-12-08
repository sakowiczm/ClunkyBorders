using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders
{
    internal class BorderManager
    {
        /*
        todo:
            Current thinking - create new transparent window on top of the selected window
            draw the border directly on that window & ensure it's click through (WS_EX_TRANSPARENT)

        https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexa
        https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexa
        https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles
        https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles

        */

        private const string OverlayWindowClassName = "ClunkyBordersOverlayClass";
        private const string OverlayWindowName = "ClunkyBordersOverlay";

        private HWND overlayWindow;

        public void Init()
        {
            try
            {
                var hInstance = GetCurrentModuleInstance();

                RegisterWindowClass(hInstance);
                overlayWindow = CreateWindow(hInstance);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error initializing overlay window: {ex}");
            }
        }

        public void Show(WindowInfo window)
        {
            Console.WriteLine($"Show border -> {window.ToString()}");

            if (overlayWindow.IsNull)
            {
                // todo: error
                return;
            }

            // https://learn.microsoft.com/en-gb/windows/win32/api/winuser/nf-winuser-setwindowpos

            PInvoke.SetWindowPos(
                overlayWindow,
                new HWND(-1), // HWND_TOPMOST
                window.Rect.X, window.Rect.Y, window.Rect.Width, window.Rect.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

            PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);

            //Console.WriteLine($"Window size: {window.Rect.X}, {window.Rect.Y}, {window.Rect.Width}, {window.Rect.Height}");
        }

        public void Hide() 
        {
            Console.WriteLine("Hide border -> window excluded");
        }


        private HINSTANCE GetCurrentModuleInstance()
        {
            unsafe
            {
                var hModule = PInvoke.GetModuleHandle((PCWSTR)null);
                return new HINSTANCE(hModule.Value);
            }
        }

        private void RegisterWindowClass(HINSTANCE hInstance)
        {
            unsafe
            {
                fixed (char* className = OverlayWindowClassName) // so GC does not move the string
                {
                    PInvoke.RegisterClassEx(new WNDCLASSEXW
                    {
                        cbSize = (uint)sizeof(WNDCLASSEXW),
                        style = 0,
                        lpfnWndProc = WindowProc,
                        cbClsExtra = 0,
                        cbWndExtra = 0,
                        hInstance = hInstance,
                        hIcon = HICON.Null,
                        hCursor = HCURSOR.Null,
                        hbrBackground = new HBRUSH(5),
                        lpszMenuName = null,
                        lpszClassName = className,
                        hIconSm = HICON.Null
                    });

                    // todo: GetLastError
                }
            }
        }

        private LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private HWND CreateWindow(HINSTANCE hInstance)
        {
            HWND wHwnd;

            unsafe
            {
                fixed(char* pClassName = OverlayWindowClassName)
                fixed(char* pWindowName = OverlayWindowName)
                {
                    wHwnd = PInvoke.CreateWindowEx(
                        WINDOW_EX_STYLE.WS_EX_TRANSPARENT | // Mouse pass through below window
                        WINDOW_EX_STYLE.WS_EX_TOPMOST |     // Always on top
                        WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |  // No taskbar
                        WINDOW_EX_STYLE.WS_EX_NOACTIVATE,   // Can't get focus
                        pClassName,
                        pWindowName,
                        WINDOW_STYLE.WS_POPUP,              // No borders, no title bar
                        0, 0, 1, 1,                         // We will resize the window later
                        HWND.Null,                          // No parent window
                        HMENU.Null,                         // No menu
                        hInstance,
                        null);

                    // todo: GetLastError
                }
            }

            return wHwnd;
        }
    }


}
