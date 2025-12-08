using Windows.Win32.Foundation;

namespace ClunkyBorders
{
    internal record WindowInfo
    {
        public required HWND Handle { get; init; }

        public required string ClassName { get; init; }

        public required string Text { get; init; }

        public required RECT Rect { get; init; }

        public override string ToString()
        {
            return $"""
                    Window: 
                        Class Name: {(string.IsNullOrEmpty(ClassName) ? "FAIL" : ClassName)} 
                        Text: {(string.IsNullOrEmpty(Text) ? "FAIL" : Text)}
                        HWND: {Handle}
                    """;
        }
    }
}
