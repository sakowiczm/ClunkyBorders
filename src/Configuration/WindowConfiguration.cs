namespace ClunkyBorders.Configuration;

internal class WindowConfiguration
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
