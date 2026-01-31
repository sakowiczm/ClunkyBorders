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
    public uint Color { get; set; } = 0xFFFFA500;
    public int Width { get; set; } = 4;
    public int Offset { get; set; } = 0;

    // todo: move to other config class?
    public bool EnableBitmapCaching { get; set; } = true;
    // todo: read from config file - todo: move to WindowConfig
    public int ValidationInterval { get; set; } = 250;
    public bool EnableAnimations { get; set; } = false;
    public int AnimationDuration { get; set; } = 150;

    public bool IsValid => Color > 0 && Width > 1 && ValidationInterval > 0;
}

internal record class WindowConfig
{
    public HashSet<string> ExcludedClassNames = new HashSet<string>()
    {
        "Windows.UI.Core.CoreWindow",               // Windows Start menu
        "Shell_TrayWnd",                            // Windows taskbar
        "TopLevelWindowForOverflowXamlIsland",      // Windows tray show hidden icons
        "XamlExplorerHostIslandWindow",             // Windows Task Swicher
        "ForegroundStaging",                        // Windows Task Swicher - temporary window
        "Progman",                                  // Program Manager - e.g when clicking a desktop
        "WorkerW"                                   // Windows Desktop
    };

    public bool IsValid => true;
}
