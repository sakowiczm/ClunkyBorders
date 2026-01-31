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

    private static int Main(string[] args)
    {
        Logger.Info($"ClunkyBorder Starting");
        Logger.Info($"Log file: {Logger.LogFilePath}");
        Logger.Info($"OS Version: {Environment.OSVersion}");

        using var instanceManager = new InstanceManager();

        if(!instanceManager.IsSingleInstance())
        {
            Logger.Warning("Another instance is already running. Exiting.");
            return 1;
        }

        var config = ConfigManager.Load();

        var iconLoader = new IconLoader();
        using var borderRenderer = new BorderRenderer(config.Border);
        using var windowValidator = new WindowValidator(config.Border.ValidationInterval);
        using var trayManager = new TrayManager(iconLoader);
        using var windowMonitor = new WindowMonitor();

        windowValidator.WindowInvalidated += (sender, window) =>
        {
            Logger.Debug($"Main. Border invalidated for window: {window.ClassName}. Hiding border.");
            borderRenderer.Hide();
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

                if (windowInfo != null && config.Window.ExcludedClassNames.Contains(windowInfo.ClassName))
                {
                    Logger.Debug($"Main. Excluding window. {windowInfo}");
                    return;
                }

                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    // todo: describe / add to configuration
                    await DelayIfWindowIsNotReady(windowInfo, 30, 700);

                    if (ct.IsCancellationRequested) return;

                    // Verify the window is still the foreground window before showing border
                    // Filters out brief focus changes comming from other windows
                    if (!windowInfo.IsValidForBorder())
                    {
                        Logger.Debug($"Main. Window {windowInfo.ClassName} is not valid for border (not foreground or not ready). Skipping border.");
                        return;
                    }

                    borderRenderer.Show(windowInfo);
                    windowValidator.Start(windowInfo);
                }
                else
                {
                    Logger.Info($"Main. Hiding border {windowInfo}");
                    windowValidator.Stop();
                    borderRenderer.Hide();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected - new window event came in
            }
            catch
            {
                Logger.Error($"Main. Error handling WindowChanged event.");
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