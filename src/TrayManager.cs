using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders
{
    // todo: add logging 
    // todo: add About window?

    internal class TrayManager : IDisposable
    {
        private const string TrayIconWindowClass = "ClunkyBorderTrayIconWindowClass";
        private const string OverlayWindowName = "ClunkyBorderTrayWindow";

        private const int WM_APP_TRAYICON = 0x8000;
        public const int MENU_EXIT = 1001;

        private static HMENU hMenu;
        private static NOTIFYICONDATAW notifyIconData;

        private bool disposed = false;

        public TrayManager() 
        {
            var hwnd = CreateWindow();
            notifyIconData = CreateTrayIcon(hwnd);
            CreateMenu();
        }

        private unsafe HWND CreateWindow()
        {
            HWND wHwnd;

            var hModule = PInvoke.GetModuleHandle((PCWSTR)null);
            var hInstance = new HINSTANCE(hModule.Value);

            fixed (char* pClassName = TrayIconWindowClass)
            fixed (char* pWindowName = OverlayWindowName)
            {
                var atom = PInvoke.RegisterClassEx(new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = WndProc,
                    hInstance = hInstance,
                    lpszClassName = pClassName
                });

                if (atom == IntPtr.Zero)
                {
                    //logger.Error($"TrayManager. Error registering window class. Error code: {Marshal.GetLastWin32Error()}");
                    return default;
                }

                // Message only window
                wHwnd = PInvoke.CreateWindowEx(
                    0,                              
                    pClassName,
                    pWindowName,
                    0,                              
                    0, 0, 0, 0,
                    HWND.HWND_MESSAGE,
                    HMENU.Null,
                    hInstance,
                    null
                );

                if (wHwnd.IsNull)
                {
                    //logger.Error($"TrayManager. Error creating window. Error code: {Marshal.GetLastWin32Error()}");
                    return default;
                }

                return wHwnd;
            }
        }

        private unsafe NOTIFYICONDATAW CreateTrayIcon(HWND hwnd)
        {
            var notifyIconData = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = hwnd, // our window do receive notifications
                uID = 1,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                uCallbackMessage = WM_APP_TRAYICON,
                hIcon = IconLoader.LoadFromResources("icon.ico"),
                szTip = "ClunkyBorders" // todo: configuration
            };

            // Add the icon to the system tray
            var result = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, notifyIconData);

            // If failed try modify
            if (!result)
            {
                PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, notifyIconData);
            }

            return notifyIconData;
        }

        private unsafe void CreateMenu()
        {
            hMenu = PInvoke.CreatePopupMenu();

            // todo: configuration
            fixed (char* pExit = "Exit")
            {
                PInvoke.AppendMenu(hMenu, MENU_ITEM_FLAGS.MF_STRING, MENU_EXIT, pExit);
            }
        }

        private unsafe static int ShowMenu(HWND hwnd)
        {
            // Required to dismiss menu properly
            PInvoke.SetForegroundWindow(hwnd);

            if (PInvoke.GetCursorPos(out var pt))
            {
                return PInvoke.TrackPopupMenuEx(
                    hMenu,
                    (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON),
                    pt.X,
                    pt.Y,
                    hwnd,
                    null
                );
            }
            return 0;
        }

        private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            // if this is our tray icon & check right mouse button was release
            if (msg == WM_APP_TRAYICON && (int)lParam.Value == PInvoke.WM_RBUTTONUP)
            {
                var cmd = ShowMenu(hwnd);

                if (cmd == MENU_EXIT)
                    PInvoke.PostQuitMessage(0);

                return new LRESULT(0);
            }

            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private void Destroy()
        {
            // Remove tray icon
            if (!notifyIconData.hWnd.IsNull)
            {
                PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, notifyIconData);
            }

            // Remove menu
            if (!hMenu.IsNull)
            {
                PInvoke.DestroyMenu(hMenu);
                hMenu = default;
            }

            // todo: DestroyWindow
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

            Destroy();

            disposed = true;
        }

        ~TrayManager()
        {
            Dispose(false);
        }

    }
}
