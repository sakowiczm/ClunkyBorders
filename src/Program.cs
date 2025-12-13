using ClunkyBorders;
using Windows.Win32;
using Windows.Win32.Foundation;


// NEXT
// - draw border only for parent windows filter dialogs, splash screens etc.
// - extract configuration class
// - AOT 
// - app should have only one instance
// - better error handling / logging
// - log to file
// - Add rounded corners to the border
// - Windows Applicaiton - ability to close it 
// - windowDetector.Stop() - it's not reachable right now

// Issues:
// - When window is transition between different monitors - border window has odd placement
// - Border is drawn over window task bar

internal class Program
{
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
                if (windowInfo != null && windowInfo.State == WindowState.Normal)
                    borderManager.Show(windowInfo);
                else
                    borderManager.Hide();
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