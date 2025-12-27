namespace ClunkyBorders.Configuration;

internal record Config
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

    public bool Validate()
    {
        // todo: return validation errors so we can log it
        return true;
    }
}

internal record BorderConfig
{
    public uint Color { get; set; } = 0xFFFFA500;
    public int Width { get; set; } = 4;
    public int Gap { get; set; } = 0;
    public int Radius { get; set; } = 0;
}

internal record WindowConfig
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
}
