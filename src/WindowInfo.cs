using Windows.Win32.Foundation;

namespace ClunkyBorders
{
    internal record WindowInfo
    {
        public required HWND Handle { get; init; }

        public required string ClassName { get; init; }

        public required string Text { get; init; }

        public RECT Rect { get; init; }

        public WindowState State { get; init; }

        public bool IsParent { get; init; }

        public uint DPI { get; init; }

        public bool CanHaveBorder()
        {
            // todo: check RECT
            return State == WindowState.Normal && IsParent;
        }

        public override string ToString()
        {
            return $"""
                    Window ({Handle}): 
                        Class Name: {(string.IsNullOrEmpty(ClassName) ? "FAIL" : ClassName)} 
                        Text: {(string.IsNullOrEmpty(Text) ? "FAIL" : Text)}
                        State: {State.ToString()}
                        IsParent: {IsParent}
                        DPI: {DPI}
                        Rect: {Rect.left}, {Rect.top}, {Rect.right}, {Rect.bottom}
                    """;
        }
    }

    enum WindowState
    {
        Hiden = 0,
        Normal = 1,
        Minimized = 2,
        Maximized = 3,
        Unknown = 4
    };
}