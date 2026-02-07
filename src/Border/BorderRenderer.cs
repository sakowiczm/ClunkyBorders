using ClunkyBorders.Common;
using ClunkyBorders.Configuration;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClunkyBorders.Border;

internal class BorderRenderer : IDisposable
{
    private const string OverlayWindowClassName = "ClunkyBordersOverlayClass";
    private const string OverlayWindowName = "ClunkyBordersOverlayWindow";
    private const int DEFAULT_SCREEN_DPI = 96; // 100%

    private HWND overlayWindow;
    private bool isWindowVisible = false;

    private readonly BorderConfig borderConfiguration = null!;
    private readonly BitmapCache _bitmapCache = null!;

    private bool disposed = false;

    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }

    public unsafe BorderRenderer(BorderConfig borderConfig)
    {
        try
        {
            this.borderConfiguration = borderConfig ?? throw new ArgumentNullException(nameof(borderConfig));

            _bitmapCache = new BitmapCache(
                borderConfig.EnableBitmapCaching,
                maxSize: 20);

            EnableDpiAwarness();

            overlayWindow = CreateWindow();
        }
        catch (Exception ex)
        {
            Logger.Error("BorderRenderer. Error initializing overlay window.", ex);
        }
    }

    public void Show(Window window)
    {
        Logger.Info($"BorderRenderer. Show border: {window.ToString()}\n\r");

        if (overlayWindow.IsNull)
            return;

        var overlayRect = window.GetOverlayRect(borderConfiguration.Offset);

        // Position window first
        PInvoke.SetWindowPos(
            overlayWindow,
            HWND.HWND_TOPMOST,
            overlayRect.X, overlayRect.Y, overlayRect.Width, overlayRect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        if (!isWindowVisible)
        {
            PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
            isWindowVisible = true;
        }

        DrawBorder(overlayRect.Width, overlayRect.Height, window.DPI, 255);

        Logger.Debug($"""
                      BorderRenderer. Border shown.
                        Original: {window.Rect.X}, {window.Rect.Y}, {window.Rect.Width}, {window.Rect.Height}
                        Adjusted: {overlayRect.X}, {overlayRect.Y}, {overlayRect.Width}, {overlayRect.Height}
                     """);
    }

    // Offset = 0 - border is drawn inside existing window size 

    // if Offset is != 0 then border size is adjusted by offset size on each side. Meaning original active window size is extended by offset and then border is drawn on newly created RECT.
    // This way you can achieve different effect e.g.
    //  border is drawn fully outside active window (e.g with a offset) e.g. offset = 6, width = 3 - you get 3px offset between 3px width border and active window
    //  border is drawn partially outside partially inside the active window e.g offset = 3, width = 6 - 3px of border is drawn outside active window, and 3px is drawn inside the active window location.
    //  border is drawn fully inside the area of active window with border between window and the border - offset = -3px and width = 3px

    public void Hide()
    {
        if (overlayWindow.IsNull)
        {
            Logger.Warning("BorderRenderer. Cant hide border - overlay window is null.");
            return;
        }

        if (!isWindowVisible)
        {
            Logger.Debug("BorderRenderer. Border already hidden, skipping hide operation.");
            return;
        }

        PInvoke.ShowWindow(overlayWindow, SHOW_WINDOW_CMD.SW_HIDE);
        isWindowVisible = false;

        Logger.Debug("BorderRenderer. Border hidden.");
    }

    private unsafe void DrawBorder(int width, int height, uint dpi, byte alpha = 255)
    {
        try
        {
            // Screen device context
            var screenDc = PInvoke.GetDC(HWND.Null);
            if (screenDc == default)
            {
                Logger.Error($"BorderRenderer. Failed to get screen DC. Error code: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                var memoryDc = PInvoke.CreateCompatibleDC(screenDc);
                if (memoryDc == default)
                {
                    Logger.Error($"BorderRenderer. Failed to create compatible DC. Error code: {Marshal.GetLastWin32Error()}");
                    return;
                }

                try
                {
                    // Get or create cached bitmap
                    var hBitmap = GetOrCreateBitmap(width, height, dpi, memoryDc, out void* pBits, out bool isCached);
                    if (hBitmap.IsNull)
                    {
                        Logger.Error($"BorderRenderer. Failed to create DIB section. Error code: {Marshal.GetLastWin32Error()}");
                        return;
                    }

                    var oldBitmap = PInvoke.SelectObject(memoryDc, hBitmap);

                    // Only redraw pixels if this is a new bitmap (not from cache)
                    if (!isCached)
                    {
                        SetPixels(width, height, dpi, (uint*)pBits);
                    }

                    var size = new SIZE { cx = width, cy = height };
                    var winPtSrc = new System.Drawing.Point(0, 0);
                    var blend = new BLENDFUNCTION
                    {
                        BlendOp = 0x00,
                        BlendFlags = 0,
                        SourceConstantAlpha = alpha,
                        AlphaFormat = 0x01
                    };

                    if (!PInvoke.UpdateLayeredWindow(overlayWindow, default, null, &size, memoryDc,
                        &winPtSrc, new COLORREF(0), &blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA))
                    {
                        Logger.Error($"BorderRenderer. UpdateLayeredWindow failed. Error code: {Marshal.GetLastWin32Error()}");
                    }

                    PInvoke.SelectObject(memoryDc, oldBitmap);

                    // Delete bitmap if caching is disabled, otherwise keep in cache
                    if (!borderConfiguration.EnableBitmapCaching)
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
            Logger.Error("BorderRenderer. Error during RenderBorder.", ex);
        }
    }
    private static float GetScaleFactor(uint dpi) => dpi == 0 ? 1 : (float)dpi / DEFAULT_SCREEN_DPI;

    private unsafe void SetPixels(int width, int height, uint dpi, uint* pixels)
    {
        var span = new Span<uint>(pixels, width * height);
        span.Clear(); // Faster than loop for zeroing

        uint borderColor = borderConfiguration.Color;
        int border = (int)(borderConfiguration.Width * GetScaleFactor(dpi));

        // Top and bottom borders
        for (int y = 0; y < border; y++)
        {
            span.Slice(y * width, width).Fill(borderColor);
            span.Slice((height - y - 1) * width, width).Fill(borderColor);
        }

        // Left and right borders (skip corners already drawn)
        for (int y = border; y < height - border; y++)
        {
            for (int x = 0; x < border; x++)
            {
                span[y * width + x] = borderColor;
                span[y * width + (width - x - 1)] = borderColor;
            }
        }
    }

    private unsafe void EnableDpiAwarness()
    {
        try
        {
            var result = PInvoke.SetProcessDpiAwarenessContext((DPI_AWARENESS_CONTEXT)(-4));

            if (result == false)
                Logger.Error($"BorderRenderer. Error setting DPI awarness. Error Code: {Marshal.GetLastWin32Error()}");
            else
                Logger.Info("BorderRenderer. DPI awarness enabled");
        }
        catch (Exception ex)
        {
            Logger.Error("BorderRenderer. Error enabling DPI awarness", ex);
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
                Logger.Error($"BorderRenderer. Error registering window class. Error code: {Marshal.GetLastWin32Error()}");
                return default;
            }

            wHwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TRANSPARENT |     // Mouse pass through below window
                WINDOW_EX_STYLE.WS_EX_TOPMOST |     // Always on top
                WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |     // No taskbar
                WINDOW_EX_STYLE.WS_EX_NOACTIVATE |     // Can't get focus
                WINDOW_EX_STYLE.WS_EX_LAYERED,          // Transparency
                pClassName,
                pWindowName,
                WINDOW_STYLE.WS_POPUP,              // No borders, no title bar
                0, 0, 1, 1,                         // We will resize the window later
                HWND.Null,                          // No parent window
                HMENU.Null,                         // No menu
                hInstance,
                null);

            // set rounded corners
            int cornerPreference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            PInvoke.DwmSetWindowAttribute(
                wHwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &cornerPreference,
                sizeof(int)
            );

            if (wHwnd.IsNull)
            {
                Logger.Error($"BorderRenderer. Error creating window. Error code: {Marshal.GetLastWin32Error()}");
                return default;
            }
        }

        return wHwnd;
    }

    private unsafe HBITMAP GetOrCreateBitmap(int width, int height, uint dpi, HDC memoryDc, out void* pBits, out bool isCached)
    {
        var bitmap = _bitmapCache.GetOrCreate(width, height, dpi, memoryDc,
            CreateBitmap,
            out var pixelBuffer,
            out isCached);

        pBits = (void*)pixelBuffer;
        return bitmap;
    }

    private unsafe (HBITMAP bitmap, IntPtr pixelBuffer) CreateBitmap(int width, int height, uint dpi, HDC memoryDc)
    {
        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down bitmap
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

        void* bits;

        // Device independent bitmap
        var hBitmap = PInvoke.CreateDIBSection(memoryDc, &bmi, 0, &bits, HANDLE.Null, 0);

        if (hBitmap.IsNull)
        {
            Logger.Error($"BorderRenderer. Failed to create DIB section. Error code: {Marshal.GetLastWin32Error()}");
            return (default, IntPtr.Zero);
        }

        return (hBitmap, (IntPtr)bits);
    }

    private void Destroy()
    {
        var success = PInvoke.DestroyWindow(overlayWindow);
        if (!success)
        {
            Logger.Error($"BorderRenderer. Error destroying window. Error code: {Marshal.GetLastWin32Error()}");
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

        if (disposing)
        {
            _bitmapCache?.Dispose();
        }

        Destroy();

        disposed = true;
    }

    ~BorderRenderer()
    {
        Dispose(false);
    }

}