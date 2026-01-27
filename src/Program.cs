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
        using var focusMonitor = new ActiveWindowMonitor();

        focusMonitor.WindowChanged += (sender, windowInfo) =>
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

        focusMonitor.Start();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }       

        focusMonitor.Stop();

        return 0;
    }
}