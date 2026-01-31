using ClunkyBorders.Common;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace ClunkyBorders.Border;

internal class BitmapCache : IDisposable
{
    private readonly Dictionary<(int width, int height, uint dpi), (HBITMAP bitmap, IntPtr pixelBuffer)> _cache = new();
    private readonly int _maxSize;
    private readonly bool _enabled;
    private bool _disposed;

    public int Count => _cache.Count;

    public BitmapCache(bool enabled, int maxSize = 20)
    {
        _enabled = enabled;
        _maxSize = maxSize;
    }

    public unsafe HBITMAP GetOrCreate(int width, int height, uint dpi, HDC memoryDc,
        Func<int, int, uint, HDC, (HBITMAP bitmap, IntPtr pixelBuffer)> createBitmatFunc,
        out IntPtr pixelBuffer, out bool isCached)
    {
        if (!_enabled)
        {
            var (bitmap, buffer) = createBitmatFunc(width, height, dpi, memoryDc);
            pixelBuffer = buffer;
            isCached = false;
            return bitmap;
        }

        var key = (width, height, dpi);

        if (_cache.TryGetValue(key, out var cached))
        {
            pixelBuffer = cached.pixelBuffer;
            isCached = true;
            Logger.Debug($"BitmapCache. Cache HIT for {width}×{height} @ {dpi} DPI");
            return cached.bitmap;
        }

        var (newBitmap, newBuffer) = createBitmatFunc(width, height, dpi, memoryDc);

        if (newBitmap.IsNull)
        {
            pixelBuffer = IntPtr.Zero;
            isCached = false;
            return newBitmap;
        }

        // Evict oldest if at capacity
        if (_cache.Count >= _maxSize)
        {
            var oldest = _cache.First();
            Logger.Debug($"BitmapCache. Evicting {oldest.Key.width}×{oldest.Key.height} @ {oldest.Key.dpi} DPI");
            PInvoke.DeleteObject(oldest.Value.bitmap);
            _cache.Remove(oldest.Key);
        }

        _cache[key] = (newBitmap, newBuffer);
        pixelBuffer = newBuffer;
        isCached = false;
        Logger.Debug($"BitmapCache. Cache MISS - created {width}×{height} @ {dpi} DPI (size: {_cache.Count})");

        return newBitmap;
    }

    public void Clear()
    {
        foreach (var (bitmap, _) in _cache.Values)
        {
            PInvoke.DeleteObject(bitmap);
        }
        _cache.Clear();
        Logger.Debug("BitmapCache. Cleared all cached bitmaps");
    }

    public void Dispose()
    {
        if (_disposed) return;

        var count = _cache.Count;
        Clear();
        Logger.Debug($"BitmapCache. Disposed {count} cached bitmaps");

        _disposed = true;
    }
}
