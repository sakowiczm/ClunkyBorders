using ClunkyBorders;
using Windows.Win32;
using Windows.Win32.Foundation;

internal class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine($"ClunkyBorder Starting");

        // todo: app should have only one instance

        var borderManager = new BorderManager();
        borderManager.Init();

        var windowDetector = new WindowMonitor();
        windowDetector.WindowChanged += (sender, windowInfo) =>
        {
            try
            {
                if (windowInfo != null)
                    borderManager.Show(windowInfo);
                else
                    borderManager.Hide();
            }
            catch
            {
                Console.WriteLine($"Error handling WindowChanged event.");
            }

        };

        windowDetector.Start();

        Console.WriteLine($"Event loop...");

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        // todo: fix - right now it's not reachable
        windowDetector.Stop();

        return 0;
    }

}