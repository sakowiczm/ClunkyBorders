namespace ClunkyBorders
{
    internal class BorderManager
    {
/*
        todo:
            Current thinking - create new transparent window on top of the selected window
            draw the border directly on that window & ensure it's click through (WS_EX_TRANSPARENT)

        https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexa
        https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexa
        https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles
        https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
*/

        public void Show(WindowInfo window)
        {
            Console.WriteLine($"Show border -> {window.ToString()}");
        }

        public void Hide() 
        {
            Console.WriteLine("Hide border -> window excluded");
        }

    }
}
