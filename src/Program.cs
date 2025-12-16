using ClunkyBorders;
using ClunkyBorders.Configuration;
using Windows.Win32;
using Windows.Win32.Foundation;

// Todo:
// - AOT - use dotnet publish -c Release ~1.5MB
// - App should have only one instance
// - Log to file
// - Add rounded corners to the border
// - Introduce gap
// - Windows Applicaiton - ability to close it 
// - WindowDetector.Stop() - it's not reachable right now
// - Preformance - multiple mouse clicks on the same window - keep state?
// - Performance - when window class is excluded - we don't need other calls
// - How can handle elevate windows?
// - Exclude pintscreen app
//      Class Name: XamlWindow
//      Text: Snipping Tool Overlay
// - Border is drawn over window task bar - z-order issue

internal class Program
{
    private static int Main(string[] args)
    {
        var logger = new Logger();

        logger.Info($"ClunkyBorder Starting");

        var windowConfig = new WindowConfiguration();
        var borderConfig = new BorderConfiguration();
        var borderManager = new BorderRenderer(borderConfig, logger);
        var windowDetector = new WindowMonitor(logger);

        windowDetector.WindowChanged += (sender, windowInfo) =>
        {
            try
            {
                if(windowInfo != null && windowConfig.ExcludedClassNames.Contains(windowInfo.ClassName))
                {
                    logger.Debug($"Main. Excluding window. {windowInfo}");
                    return;
                }

                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    borderManager.Show(windowInfo);
                }
                else
                {
                    logger.Info($"Main. Hidding border {windowInfo}");

                    borderManager.Hide();
                }
            }
            catch
            {
                logger.Error($"Main. Error handling WindowChanged event.");
            }

        };

        windowDetector.Start();

        logger.Info($"Main. Event loop...");

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        windowDetector.Stop();

        return 0;
    }
}