using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

Console.WriteLine($"ClunkyBorder Starting");

var hookHwnd = PInvoke.SetWinEventHook(
    PInvoke.EVENT_SYSTEM_FOREGROUND,
    PInvoke.EVENT_SYSTEM_FOREGROUND,
    HMODULE.Null,
    WindowEventCallback,
    0,
    0,
    0x0000u | 0x0002u
);



Console.WriteLine($"Event loop...");

while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
{
    PInvoke.TranslateMessage(msg);
    PInvoke.DispatchMessage(msg);
}

return 0;

/*
 todo: add window filtering

Active window changed:
    Class Name: Shell_TrayWnd
    Text: FAIL
    HWND: 0x10112
Active window changed:
    Class Name: ForegroundStaging
    Text: FAIL
    HWND: 0x1016c
Active window changed:
    Class Name: XamlExplorerHostIslandWindow
    Text: Task Switching
    HWND: 0x106dc
Active window changed:
    Class Name: Windows.UI.Core.CoreWindow
    Text: Search
    HWND: 0x101dc
Active window changed:
    Class Name: TaskManagerWindow
    Text: Task Manager
    HWND: 0x160d5e
*/


void WindowEventCallback(
        HWINEVENTHOOK hWinEventHook,
        uint @event,
        HWND hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
{
    try
    {
        if (@event != PInvoke.EVENT_SYSTEM_FOREGROUND)
            return;

        var windowClassName = GetWindowClassName(hwnd);
        var windowText = GetWindowText(hwnd);

        Console.WriteLine($"""
            Active window changed: 
                Class Name: {(string.IsNullOrEmpty(windowClassName) ? "FAIL" : windowClassName)} 
                Text: {(string.IsNullOrEmpty(windowText) ? "FAIL" : windowText)}
                HWND: {hwnd}
            """);
            
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex}");
    }
}


unsafe string GetWindowClassName(HWND hwnd)
{
    const int maxLength = 256;
    var buffer = new char[maxLength];

    fixed (char* pBuffer = buffer)
    {
        var length = PInvoke.GetClassName(hwnd, pBuffer, maxLength);
        return length == 0 ? string.Empty : new string(pBuffer, 0, length);
    }
}


unsafe string GetWindowText(HWND hwnd)
{
    const int maxLength = 256;
    var buffer = new char[maxLength];

    fixed (char* pBuffer = buffer)
    {
        var length = PInvoke.GetWindowText(hwnd, pBuffer, maxLength);
        return length == 0 ? string.Empty : new string(pBuffer, 0, length);
    }
}