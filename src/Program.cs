using ClunkyBorders;
using ClunkyBorders.Configuration;
using Windows.Win32;
using Windows.Win32.Foundation;

internal class Program
{
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
        using var trayManager = new TrayManager(iconLoader);
        using var activeMonitor = new ActiveWindowMonitor();

        activeMonitor.WindowChanged += async (sender, windowInfo) =>
        {
            try
            {
                if (windowInfo != null && config.Window.ExcludedClassNames.Contains(windowInfo.ClassName))
                {
                    Logger.Debug($"Main. Excluding window. {windowInfo}");
                    return;
                }

                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    // todo: describe / add to configuration
                    await DelayIfWindowIsNotReady(windowInfo, 30, 700);

                    borderRenderer.Show(windowInfo);
                }
                else
                {
                    Logger.Info($"Main. Hidding border {windowInfo}");

                    borderRenderer.Hide();
                }
            }
            catch
            {
                Logger.Error($"Main. Error handling WindowChanged event.");
            }

        };

        activeMonitor.Start();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }       

        activeMonitor.Stop();

        return 0;
    }

    public static async Task<bool> DelayIfWindowIsNotReady(WindowInfo window, int intervalDelay = 20, int maxDelay = 300)
    {
        for (int elapsed = 0; elapsed < maxDelay; elapsed += intervalDelay)
        {
            if (window.IsWindowReady())
            {
                return true;
            }
            await Task.Delay(intervalDelay);
        }

        var isReady = window.IsWindowReady();

        if (!isReady)
            Logger.Debug($"Main. Window not ready after waiting, showing border anyway.");

        return isReady;
    }
}