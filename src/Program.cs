using ClunkyBorders;
using ClunkyBorders.Border;
using ClunkyBorders.Common;
using ClunkyBorders.Configuration;
using ClunkyBorders.Tray;
using Windows.Win32;
using Windows.Win32.Foundation;

internal class Program
{
    private static CancellationTokenSource? _cancellationTokenSource;
    private static HWND _currentBorderedWindow = HWND.Null;
    private static readonly object _borderStateLock = new object();

    private static int Main(string[] args)
    {
        Logger.Info($"ClunkyBorder Starting");
        Logger.Info($"Log file: {Logger.LogFilePath}");
        Logger.Info($"OS Version: {Environment.OSVersion}");

        using var instanceManager = new InstanceManager();

        if (!instanceManager.IsSingleInstance())
        {
            Logger.Warning("Another instance is already running. Exiting.");
            return 1;
        }

        var config = ConfigManager.Load();

        var iconLoader = new IconLoader();
        using var borderRenderer = new BorderRenderer(config.Border);
        using var windowValidator = new WindowValidator(config.Window.ValidationInterval);
        using var trayManager = new TrayManager(iconLoader);
        using var windowMonitor = new WindowMonitor();

        windowValidator.WindowInvalidated += async (sender, window) =>
        {
            // Only hide if this is the window that currently has the border
            lock (_borderStateLock)
            {
                if (_currentBorderedWindow != window.Handle)
                {
                    Logger.Debug($"Main. Border invalidated for window: {window.ClassName}, but border is for different window. Ignoring.");
                    return;
                }
            }

            Logger.Debug($"Main. Border invalidated for window: {window.ClassName}. Hiding border.");
            await borderRenderer.Hide();

            lock (_borderStateLock)
            {
                _currentBorderedWindow = HWND.Null;
            }
        };

        windowMonitor.WindowChanged += async (sender, windowInfo) =>
        {
            // Cancel previous operation
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            try
            {
                if (ct.IsCancellationRequested) return;

                if (windowInfo != null && IsWindowExcluded(windowInfo, config.Window))
                {
                    Logger.Debug($"Main. Excluding window by exclusion rule. {windowInfo}");
                    return;
                }

                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    // todo: describe / add to configuration
                    await DelayIfWindowIsNotReady(windowInfo, 30, 700, ct);

                    if (ct.IsCancellationRequested) return;

                    // Verify the window is still the foreground window before showing border
                    // Filters out brief focus changes comming from other windows
                    if (!windowInfo.IsValidForBorder())
                    {
                        Logger.Debug($"Main. Window {windowInfo.ClassName} is not valid for border (not foreground or not ready). Skipping border.");
                        return;
                    }

                    // Stop validator before showing new border to prevent old validator from hiding new border
                    windowValidator.Stop();

                    await borderRenderer.Show(windowInfo);

                    lock (_borderStateLock)
                    {
                        _currentBorderedWindow = windowInfo.Handle;
                    }

                    windowValidator.Start(windowInfo);
                }
                else
                {
                    Logger.Info($"Main. Hiding border {windowInfo}");
                    windowValidator.Stop();
                    await borderRenderer.Hide();

                    lock (_borderStateLock)
                    {
                        _currentBorderedWindow = HWND.Null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected - new window event came in
            }
            catch (Exception ex)
            {
                Logger.Error($"Main. Error handling WindowChanged event.", ex);
            }

        };

        windowMonitor.Start();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        windowMonitor.Stop();

        _cancellationTokenSource?.Dispose();

        return 0;
    }

    private static bool IsWindowExcluded(Window windowInfo, WindowConfig config)
    {
        return config.Exclusions.Any(ex =>
        {
            bool classMatch = !string.IsNullOrEmpty(ex.ClassName) &&
                string.Equals(ex.ClassName, windowInfo.ClassName, StringComparison.OrdinalIgnoreCase);
            bool textMatch = !string.IsNullOrEmpty(ex.Text) &&
                string.Equals(ex.Text, windowInfo.Text, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(ex.ClassName) && !string.IsNullOrEmpty(ex.Text))
                return classMatch && textMatch;
            if (!string.IsNullOrEmpty(ex.ClassName))
                return classMatch;
            if (!string.IsNullOrEmpty(ex.Text))
                return textMatch;
            return false;
        });
    }

    public static async Task<bool> DelayIfWindowIsNotReady(Window window, int intervalDelay, int maxDelay, CancellationToken token = default)
    {
        for (int elapsed = 0; elapsed < maxDelay; elapsed += intervalDelay)
        {
            if (window.IsValidForBorder())
                return true;

            await Task.Delay(intervalDelay, token);
        }

        var isReady = window.IsValidForBorder();
        if (!isReady)
            Logger.Debug($"Main. Window not ready after waiting, showing border anyway.");

        return isReady;
    }
}