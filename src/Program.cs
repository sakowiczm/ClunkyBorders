using ClunkyBorders;
using Windows.Win32;
using Windows.Win32.Foundation;

internal class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine($"ClunkyBorder Starting");

        // todo: app should have only one instance

        var windowDetector = new ActiveWindowDetector();
        windowDetector.WindowChanged += WindowChanged;

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

    private static void WindowChanged(object? sender, Window? window)
    {
        try
        {
            var message = window == null ? "Window excluded." : window.ToString();
            Console.WriteLine(message);
        }
        catch 
        { 
            Console.WriteLine($"Error handling WindowChanged event.");
        }
    }
}