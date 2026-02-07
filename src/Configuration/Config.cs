namespace ClunkyBorders.Configuration;

internal record class Config
{
    public BorderConfig Border { get; init; }
    public WindowConfig Window { get; init; }

    public Config()
    {
        Border = new BorderConfig();
        Window = new WindowConfig();
    }

    public Config(BorderConfig border, WindowConfig window)
    {
        Border = border;
        Window = window;
    }
    public bool IsValid => Border.IsValid && Window.IsValid;
}

internal record class BorderConfig
{
    public uint Color { get; set; }
    public int Width { get; set; }
    public int Offset { get; set; }
    public bool EnableBitmapCaching { get; set; }

    public bool IsValid => Color > 0 && Width > 1;
}

internal record class WindowExclusion
{
    public string? ClassName { get; set; }
    public string? Text { get; set; }
}

internal record class WindowConfig
{
    public List<WindowExclusion> Exclusions { get; set; } = new();
    public int ValidationInterval { get; set; }
    public bool IsValid => ValidationInterval > 0;
}
