using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders
{
    internal class BorderManager
    {
        private const string OverlayWindowClassName = "ClunkyBordersOverlayClass";
        private const string OverlayWindowName = "ClunkyBordersOverlay";

        private HWND overlayWindow;

        private bool isOverlayWindowVisible = false;

        public unsafe void Init()
        {
            try
            {
                var hModule = PInvoke.GetModuleHandle((PCWSTR)null);
                var hInstance = new HINSTANCE(hModule.Value);

                RegisterWindowClass(hInstance);
                overlayWindow = CreateWindow(hInstance);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"BorderManager -> Error initializing overlay window: {ex}");
            }
        }

        public void Show(WindowInfo window)
        {
            Console.WriteLine($"BorderManager -> Show border:\n\r {window.ToString()}");

            if (overlayWindow.IsNull)
            {
                return;
            }

            PInvoke.SetWindowPos(
                overlayWindow,
                new HWND(-1), // HWND_TOPMOST
                window.Rect.X, window.Rect.Y, window.Rect.Width, window.Rect.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

            RenderBorder(window);

            if (!isOverlayWindowVisible)
            {
                PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                isOverlayWindowVisible = true;

                Console.WriteLine($"BorderManager -> Border is shown. Size: {window.Rect.X}, {window.Rect.Y}, {window.Rect.Width}, {window.Rect.Height}");
            }
        }

        public void Hide() 
        { 
            if(!isOverlayWindowVisible || overlayWindow.IsNull)
            {
                Console.WriteLine("BorderManager -> Cant hide border as it is not visible.");
            }

            try
            {
                PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_HIDE);
                isOverlayWindowVisible = false;


                Console.WriteLine("BorderManager -> Border is hidden.");
            }
            catch (Exception ex)
            {

                Console.WriteLine($"BorderManager -> Error hidding border. Exception: {ex}.");
            }

        }

        private unsafe void RenderBorder(WindowInfo window)
        {
            try
            {
                // Screen device context
                var screenDc = PInvoke.GetDC(HWND.Null);
                if (screenDc == default)
                {
                    Console.WriteLine("BorderManager -> Failed to get screen DC.");
                }

                try
                {
                    var memoryDc = PInvoke.CreateCompatibleDC(screenDc);
                    if (memoryDc == default)
                    {
                        Console.WriteLine("BorderManager -> Failed to create compatible DC.");
                        return;
                    }

                    try
                    {
                        var bmi = new BITMAPINFO
                        {
                            bmiHeader = new BITMAPINFOHEADER
                            {
                                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                                biWidth = window.Rect.Width,
                                biHeight = -window.Rect.Height, // top-down bitmap
                                biPlanes = 1,
                                biBitCount = 32,
                                biCompression = 0,
                                biSizeImage = 0,
                                biXPelsPerMeter = 0,
                                biYPelsPerMeter = 0,
                                biClrUsed = 0,
                                biClrImportant = 0
                            }
                        };

                        void* pBits;

                        // Device independent bitmap
                        var hBitmap = PInvoke.CreateDIBSection(memoryDc, &bmi, 0, &pBits, HANDLE.Null, 0);
                        if (hBitmap.IsNull)
                        {
                            Console.WriteLine("BorderManager -> Failed to create DIB section.");
                            return;
                        }

                        try
                        {
                            var oldBitmap = PInvoke.SelectObject(memoryDc, hBitmap);

                            SetPixels(window, (uint*)pBits);

                            var size = new SIZE { cx = window.Rect.Width, cy = window.Rect.Height };
                            var winPtSrc = new System.Drawing.Point(0, 0);
                            var blend = new BLENDFUNCTION
                            {
                                BlendOp = 0x00,
                                BlendFlags = 0,
                                SourceConstantAlpha = 255,
                                AlphaFormat = 0x01
                            };

                            if (!PInvoke.UpdateLayeredWindow(overlayWindow, default, null, &size, memoryDc,
                                &winPtSrc, new COLORREF(0), &blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA))
                            {
                                Console.WriteLine($"BorderManager -> UpdateLayeredWindow failed. Error: {Marshal.GetLastWin32Error()}");
                            }

                            // clean up
                            PInvoke.SelectObject(memoryDc, oldBitmap);

                        }
                        finally
                        {
                            PInvoke.DeleteObject(hBitmap);
                        }
                        
                    }
                    finally
                    {
                        PInvoke.DeleteDC(memoryDc);
                    }
                }
                finally
                {
                    PInvoke.ReleaseDC(HWND.Null, screenDc);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BorderManager -> Error during RenderBorder, Exception: {ex}");
            }
        }

        private static unsafe void SetPixels(WindowInfo window, uint* pixels)
        {
            var pixelCount = window.Rect.Width * window.Rect.Height;
            for (int i = 0; i < pixelCount; i++)
            {
                pixels[i] = 0x00000000; // Fully transparent
            }

            uint borderColor = 0xFFFFA500;
            int w = window.Rect.Width;
            int h = window.Rect.Height;
            int border = 4;

            // Top border
            for (int y = 0; y < border; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = borderColor;

            // Bottom border
            for (int y = h - border; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = borderColor;

            // Left border
            for (int y = 0; y < h; y++)
                for (int x = 0; x < border; x++)
                    pixels[y * w + x] = borderColor;

            // Right border
            for (int y = 0; y < h; y++)
                for (int x = w - border; x < w; x++)
                    pixels[y * w + x] = borderColor;
        }

        private unsafe void RegisterWindowClass(HINSTANCE hInstance)
        {
            fixed (char* className = OverlayWindowClassName)
            {
                PInvoke.RegisterClassEx(new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    style = 0,
                    lpfnWndProc = PInvoke.DefWindowProc,
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

        private unsafe HWND CreateWindow(HINSTANCE hInstance)
        {
            HWND wHwnd;

            fixed (char* pClassName = OverlayWindowClassName)
            fixed (char* pWindowName = OverlayWindowName)
            {
                wHwnd = PInvoke.CreateWindowEx(
                    WINDOW_EX_STYLE.WS_EX_TRANSPARENT |     // Mouse pass through below window
                    WINDOW_EX_STYLE.WS_EX_TOPMOST     |     // Always on top
                    WINDOW_EX_STYLE.WS_EX_TOOLWINDOW  |     // No taskbar
                    WINDOW_EX_STYLE.WS_EX_NOACTIVATE  |     // Can't get focus
                    WINDOW_EX_STYLE.WS_EX_LAYERED,          // Transparency
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

            return wHwnd;
        }

    }

}
