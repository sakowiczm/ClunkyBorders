using ClunkyBorders;
using ClunkyBorders.Configuration;
using Windows.Win32;
using Windows.Win32.Foundation;

internal class Program
{
    private static int Main(string[] args)
    {
        var logger = new Logger();

        logger.Info($"ClunkyBorder Starting");

        var windowConfig = new WindowConfiguration();
        var borderConfig = new BorderConfiguration();

        var iconLoader = new IconLoader(logger);
        using var borderRenderer = new BorderRenderer(borderConfig, logger);
        using var trayManager = new TrayManager(iconLoader, logger);
        using var focusMonitor = new FocusMonitor(logger);

        focusMonitor.WindowChanged += (sender, windowInfo) =>
        {
            try
            {
                if (windowInfo != null && windowConfig.ExcludedClassNames.Contains(windowInfo.ClassName))
                {
                    logger.Debug($"Main. Excluding window. {windowInfo}");
                    return;
                }

                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    borderRenderer.Show(windowInfo);
                }
                else
                {
                    logger.Info($"Main. Hidding border {windowInfo}");

                    borderRenderer.Hide();
                }
            }
            catch
            {
                logger.Error($"Main. Error handling WindowChanged event.");
            }

        };

        focusMonitor.Start();

        logger.Info($"Main. Event loop...");

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }       

        focusMonitor.Stop();

        return 0;
    }
}