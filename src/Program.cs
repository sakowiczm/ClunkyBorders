using ClunkyBorders;
using ClunkyBorders.Border;
using ClunkyBorders.Common;
using ClunkyBorders.Configuration;
using ClunkyBorders.Tray;
using System.CommandLine;
using Windows.Win32;
using Windows.Win32.Foundation;

internal class Program
{
    private static CancellationTokenSource? _cancellationTokenSource;
    private static HWND _currentBorderedWindow = HWND.Null;
    private static readonly object _borderStateLock = new object();

    private static int Main(string[] args)
    {
        ConsoleManager.TryAttachToParentConsole();

        var rootCommand = new RootCommand("ClunkyBorders - Window border overlay application");

        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to configuration file (default: config.toml in executable directory)")
        {
            ArgumentHelpName = "path"
        };

        var logsOption = new Option<string?>(
            aliases: ["--logs", "-l"],
            description: "Directory for log files (default: executable directory). Can be relative or absolute path.")
        {
            ArgumentHelpName = "path"
        };

        var noLogsOption = new Option<bool>(
            aliases: ["--no-logs"],
            description: "Disable log file creation. If specified, no log files will be written.");

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(logsOption);
        rootCommand.AddOption(noLogsOption);

        rootCommand.SetHandler(Run, configOption, logsOption, noLogsOption);

        return rootCommand.Invoke(args);
    }

    private static void Run(string? configPath, string? logsDir, bool noLogs)
    {
        Logger.Info($"ClunkyBorder Starting");
        Logger.Info($"OS Version: {Environment.OSVersion}");

        // Handle logging configuration
        if (noLogs)
        {
            // Disable logging entirely
            Logger.Initialize(disableLogging: true);
        }
        else if (!string.IsNullOrEmpty(logsDir))
        {
            // Convert relative path to absolute path
            var absoluteLogsPath = Path.IsPathRooted(logsDir)
                ? logsDir
                : Path.GetFullPath(logsDir);

            if (!Directory.Exists(absoluteLogsPath))
            {
                Console.WriteLine($"Error: Log directory does not exist: {absoluteLogsPath}");
                Environment.Exit(1);
            }

            Logger.Initialize(absoluteLogsPath);
        }
        
        if (!Logger.IsLoggingDisabled)
        {
            Logger.Info($"Log file: {Logger.LogFilePath}");
        }
        else
        {
            Console.WriteLine("Info: Logging disabled");
        }       

        if (!string.IsNullOrEmpty(configPath))
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Error: Configuration file not found: {configPath}");
                Logger.Error($"Error: Configuration file not found: {configPath}");
                Environment.Exit(1);
            }
            Logger.Info($"Using configuration file: {configPath}");
        }

        using var instanceManager = new InstanceManager();

        if (!instanceManager.IsSingleInstance())
        {
            Logger.Warning("Another instance is already running. Exiting.");
            Environment.Exit(1);
        }

        var config = ConfigManager.Load(configPath ?? "");

        var iconLoader = new IconLoader();
        using var borderRenderer = new BorderRenderer(config.Border);
        using var windowValidator = new WindowValidator(config.Window.ValidationInterval);
        using var trayManager = new TrayManager(iconLoader);
        using var windowMonitor = new WindowMonitor();
        using var eventThrottler = new WindowEventThrottler();

        windowValidator.WindowInvalidated += (sender, window) =>
        {
            // Only hide if this is the window that currently has the border
            lock (_borderStateLock)
            {
                if (_currentBorderedWindow != window.Handle)
                {
                    Logger.Debug($"Main. Border invalidated for window: {window.ClassName}, but border is for different window. Ignoring.");
                    return;
                }
            }

            Logger.Debug($"Main. Border invalidated for window: {window.ClassName}. Hiding border.");
            borderRenderer.Hide();

            lock (_borderStateLock)
            {
                _currentBorderedWindow = HWND.Null;
            }
        };

        windowMonitor.WindowChanged += async (sender, windowInfo) =>
        {
            // Cancel previous operation
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            try
            {
                if (ct.IsCancellationRequested) return;

                if (windowInfo != null && IsWindowExcluded(windowInfo, config.Window))
                {
                    Logger.Debug($"Main. Excluding window by exclusion rule. {windowInfo}");
                    return;
                }

                if (windowInfo != null && windowInfo.CanHaveBorder())
                {
                    // todo: add to configuration

                    // Animaitons may delay displaying window (e.g Exploler)
                    // we don't want to display the border for window that is not yet ready
                    await DelayIfWindowIsNotReady(windowInfo, 30, 700, ct);

                    if (ct.IsCancellationRequested) return;

                    // Verify the window is still the foreground window before showing border
                    // Filters out brief focus changes comming from other windows
                    if (!windowInfo.IsValidForBorder())
                    {
                        Logger.Debug($"Main. Window {windowInfo.ClassName} is not valid for border (not foreground or not ready). Skipping border.");
                        return;
                    }

                    // Stop validator before any border operations
                    windowValidator.Stop();

                    // Handle window event with throttling
                    eventThrottler.HandleWindowEvent(
                        windowInfo,
                        immediateAction: (window) =>
                        {
                            borderRenderer.Show(window);
                            lock (_borderStateLock)
                            {
                                _currentBorderedWindow = window.Handle;
                            }
                            windowValidator.Start(window);
                        },
                        rapidEventAction: () =>
                        {
                            borderRenderer.Hide();
                            lock (_borderStateLock)
                            {
                                _currentBorderedWindow = HWND.Null;
                            }
                        },
                        delayedAction: (window) =>
                        {
                            borderRenderer.Show(window);
                            lock (_borderStateLock)
                            {
                                _currentBorderedWindow = window.Handle;
                            }
                            windowValidator.Start(window);
                        }
                    );
                }
                else
                {
                    Logger.Info($"Main. Hiding border {windowInfo}");

                    // Cancel any pending delayed actions
                    eventThrottler.CancelPending();

                    windowValidator.Stop();
                    borderRenderer.Hide();

                    lock (_borderStateLock)
                    {
                        _currentBorderedWindow = HWND.Null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected - new window event came in
            }
            catch (Exception ex)
            {
                Logger.Error($"Main. Error handling WindowChanged event.", ex);
            }

        };

        windowMonitor.Start();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        windowMonitor.Stop();

        _cancellationTokenSource?.Dispose();
    }

    private static bool IsWindowExcluded(Window windowInfo, WindowConfig config)
    {
        return config.Exclusions.Any(ex =>
        {
            bool classMatch = !string.IsNullOrEmpty(ex.ClassName) &&
                string.Equals(ex.ClassName, windowInfo.ClassName, StringComparison.OrdinalIgnoreCase);
            bool textMatch = !string.IsNullOrEmpty(ex.Text) &&
                string.Equals(ex.Text, windowInfo.Text, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(ex.ClassName) && !string.IsNullOrEmpty(ex.Text))
                return classMatch && textMatch;
            if (!string.IsNullOrEmpty(ex.ClassName))
                return classMatch;
            if (!string.IsNullOrEmpty(ex.Text))
                return textMatch;
            return false;
        });
    }

    public static async Task<bool> DelayIfWindowIsNotReady(Window window, int intervalDelay, int maxDelay, CancellationToken token = default)
    {
        for (int elapsed = 0; elapsed < maxDelay; elapsed += intervalDelay)
        {
            if (window.IsValidForBorder())
                return true;

            await Task.Delay(intervalDelay, token);
        }

        var isReady = window.IsValidForBorder();
        if (!isReady)
            Logger.Debug($"Main. Window not ready after waiting, showing border anyway.");

        return isReady;
    }
}