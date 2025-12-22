using ClunkyBorders.Configuration;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders;

internal class BorderRenderer : IDisposable
{
    private const string OverlayWindowClassName = "ClunkyBordersOverlayClass";
    private const string OverlayWindowName = "ClunkyBordersOverlayWindow";
    private const int DEFAULT_SCREEN_DPI = 96; // 100%       

    private HWND overlayWindow;
    private bool isWindowVisible = false;

    private readonly BorderConfiguration borderConfiguration = null!;
    private readonly Logger logger = null!;

    private bool disposed = false;

    public unsafe BorderRenderer(BorderConfiguration borderConfig, Logger logger)
    {
        try
        {
            this.borderConfiguration = borderConfig ?? throw new ArgumentNullException(nameof(borderConfig));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            EnableDpiAwarness();

            overlayWindow = CreateWindow();
        }
        catch (Exception ex)
        {
            logger.Error($"BorderRenderer. Error initializing overlay window.", ex);
        }
    }

    public void Show(WindowInfo window)
    {
        logger.Info($"BorderRenderer. Show border:\n\r {window.ToString()}");

        if (overlayWindow.IsNull)
        {
            return;
        }

        DrawBorder(window);

        PInvoke.SetWindowPos(
            overlayWindow,
            HWND.HWND_TOPMOST,
            window.Rect.X, window.Rect.Y, window.Rect.Width, window.Rect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

        if (!isWindowVisible)
        {
            PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
            isWindowVisible = true;

            logger.Debug($"BorderRenderer. Border is shown. Size: {window.Rect.X}, {window.Rect.Y}, {window.Rect.Width}, {window.Rect.Height}");
        }
    }

    public void Hide() 
    { 
        if(!isWindowVisible || overlayWindow.IsNull)
        {
            logger.Warning("BorderRenderer. Cant hide border as it is not visible.");
        }

        try
        {
            PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_HIDE);
            isWindowVisible = false;

            logger.Debug("BorderRenderer. Border is hidden.");
        }
        catch (Exception ex)
        {
            logger.Error($"BorderRenderer. Error hidding border.", ex);
        }
    }

    private unsafe void DrawBorder(WindowInfo window)
    {
        try
        {
            // Screen device context
            var screenDc = PInvoke.GetDC(HWND.Null);
            if (screenDc == default)
            {
                logger.Error($"BorderRenderer. Failed to get screen DC. Error code: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                var memoryDc = PInvoke.CreateCompatibleDC(screenDc);
                if (memoryDc == default)
                {
                    logger.Error($"BorderRenderer. Failed to create compatible DC. Error code: {Marshal.GetLastWin32Error()}");
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
                        logger.Error($"BorderRenderer. Failed to create DIB section. Error code: {Marshal.GetLastWin32Error()}");
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
                            logger.Error($"BorderRenderer. UpdateLayeredWindow failed. Error code: {Marshal.GetLastWin32Error()}");
                        }

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
            logger.Error($"BorderRenderer. Error during RenderBorder.", ex);
        }
    }

    private static float GetScaleFactor(uint dpi) => dpi == 0 ? 1 : (float)dpi / DEFAULT_SCREEN_DPI;

    private unsafe void SetPixels(WindowInfo window, uint* pixels)
    {
        var pixelCount = window.Rect.Width * window.Rect.Height;
        for (int i = 0; i < pixelCount; i++)
        {
            pixels[i] = 0x00000000; // Fully transparent
        }

        uint borderColor = borderConfiguration.Color;
        int w = window.Rect.Width;
        int h = window.Rect.Height;

        // scale border to current screen DPI
        int border = (int)(borderConfiguration.Thickness * GetScaleFactor(window.DPI));

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

    private unsafe void EnableDpiAwarness()
    {
        try
        {
            var result = PInvoke.SetProcessDpiAwarenessContext((DPI_AWARENESS_CONTEXT)(-4));

            if(result == false)
                logger.Error($"BorderRenderer. Error setting DPI awarness. Error Code: {Marshal.GetLastWin32Error()}");
            else
                logger.Info($"BorderRenderer. DPI awarness enabled");
        }
        catch (Exception ex)
        {

            logger.Error($"BorderRenderer. Error enabling DPI awarness", ex);
        }
    }

    private unsafe HWND CreateWindow()
    {
        HWND wHwnd;

        var hModule = PInvoke.GetModuleHandle((PCWSTR)null);
        var hInstance = new HINSTANCE(hModule.Value);

        fixed (char* pClassName = OverlayWindowClassName)
        fixed (char* pWindowName = OverlayWindowName)
        {
            var atom = PInvoke.RegisterClassEx(new WNDCLASSEXW
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
                lpszClassName = pClassName,
                hIconSm = HICON.Null
            });

            if (atom == IntPtr.Zero)
            {
                logger.Error($"BorderRenderer. Error registering window class. Error code: {Marshal.GetLastWin32Error()}");
                return default;
            }

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

            if (wHwnd.IsNull)
            {
                logger.Error($"BorderRenderer. Error creating window. Error code: {Marshal.GetLastWin32Error()}");
                return default;
            }
        }

        return wHwnd;
    }

    private void Destroy()
    {
        var success = PInvoke.DestroyWindow(overlayWindow);
        if (!success)
        {
            logger.Error($"BorderRenderer. Error destroying window. Error code: {Marshal.GetLastWin32Error()}");
        }
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

    ~BorderRenderer()
    {
        Dispose(false);
    }

}
