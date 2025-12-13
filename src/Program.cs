using ClunkyBorders;
using Windows.Win32;
using Windows.Win32.Foundation;

// Todo:
// - Boundaries for some windows are off why?
// - Extract configuration class
// - AOT 
// - App should have only one instance
// - Better error handling / logging
// - Log to file
// - Add rounded corners to the border
// - Windows Applicaiton - ability to close it 
// - WindowDetector.Stop() - it's not reachable right now
// - Preformance - multiple mouse clicks on the same window - keep state?
// - Performance - when window class is excluded - we don't need other calls
// - How can handle elevate windows?
// - Exclude pintscreen app
//      Class Name: XamlWindow
//      Text: Snipping Tool Overlay
// - When window is transition between different monitors - border window has odd placement
// - Border is drawn over window task bar

internal class Program
{
    static HashSet<string> classNamesToExclude = new HashSet<string>()
        {
            "Windows.UI.Core.CoreWindow",               // Windows Start menu
            "Shell_TrayWnd",                            // Windows taskbar
            "TopLevelWindowForOverflowXamlIsland",      // Windows tray show hidden icons
            "XamlExplorerHostIslandWindow",             // Windows Task Swicher
            "ForegroundStaging",                        // Windows Task Swicher - temporary window
            "Progman"                                   // Program Manager - e.g when clicking a desktop
        };

    private static int Main(string[] args)
    {
        Console.WriteLine($"ClunkyBorder Starting");

        var borderManager = new BorderManager();
        borderManager.Init();

        var windowDetector = new WindowMonitor();
        windowDetector.WindowChanged += (sender, windowInfo) =>
        {
            try
            {
                if(windowInfo != null && classNamesToExclude.Contains(windowInfo.ClassName))
                {
                    Console.WriteLine($"Main -> Excluding window. {windowInfo}");
                    return;
                }


                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    borderManager.Show(windowInfo);
                }
                else
                {
                    Console.WriteLine($"Main -> Hidding border {windowInfo}");

                    borderManager.Hide();
                }
            }
            catch
            {
                Console.WriteLine($"Main -> Error handling WindowChanged event.");
            }

        };

        windowDetector.Start();

        Console.WriteLine($"Main -> Event loop...");

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        windowDetector.Stop();

        return 0;
    }

}