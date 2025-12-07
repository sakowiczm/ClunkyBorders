using Windows.Win32.Foundation;

namespace ClunkyBorders
{
    internal record Window
    {
        public required HWND Handle { get; init; }

        public required string ClassName { get; init; }

        public required string Text { get; init; }

        public override string ToString()
        {
            return $"""
                    Active window changed: 
                        Class Name: {(string.IsNullOrEmpty(ClassName) ? "FAIL" : ClassName)} 
                        Text: {(string.IsNullOrEmpty(Text) ? "FAIL" : Text)}
                        HWND: {Handle}
                    """;
        }
    }
}
