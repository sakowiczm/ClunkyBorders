using ClunkyBorders.Common;

namespace ClunkyBorders;

/// <summary>
/// Not every window can get border - even with evevated privileges.
/// Just hide border if it is not present on active window.
/// </summary>
internal class WindowValidator : IDisposable
{
    public event EventHandler<Window>? WindowInvalidated;

    private readonly int _validationInterval;
    private Window? _currentWindow = null;
    private Timer? _validationTimer;
    private bool _disposed = false;
    private bool _isRunning = false;
    private readonly object _lock = new object();

    public WindowValidator(int validationInterval = 250)
    {
        _validationInterval = validationInterval;
        _validationTimer = new Timer(ValidateBorderStillValid, null, Timeout.Infinite, Timeout.Infinite);

        Logger.Info($"WindowValidator. Initialized with validation interval: {_validationInterval}ms");
    }

    public void Start(Window window)
    {
        lock (_lock)
        {
            _currentWindow = window;
            _isRunning = true;
            _validationTimer?.Change(_validationInterval, _validationInterval);
            Logger.Debug($"WindowValidator. Started monitoring window: {window.ClassName}");
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _validationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        // Wait briefly for any in-flight callback to see _isRunning flag
        Thread.Sleep(10);

        lock (_lock)
        {
            _currentWindow = null;
            Logger.Debug("WindowValidator. Stopped monitoring");
        }
    }

    private void ValidateBorderStillValid(object? state)
    {
        try
        {
            if (_disposed)
                return;
            
            Window? windowToValidate;

            lock (_lock)
            {
                // If validator is not running or no window is being monitored, nothing to validate
                if (!_isRunning || _currentWindow == null)
                {
                    return;
                }

                windowToValidate = _currentWindow;
            }

            // Check if the bordered window is still the foreground window (outside lock to avoid blocking)
            if (!windowToValidate.IsForeground())
            {
                Logger.Info($"WindowValidator. Window {windowToValidate.ClassName} is no longer foreground.");

                // Double-check we're still running and monitoring the same window
                lock (_lock)
                {
                    if (!_isRunning || _currentWindow?.Handle != windowToValidate.Handle)
                    {
                        Logger.Debug($"WindowValidator. Validation cancelled - window changed or validator stopped.");
                        return;
                    }

                    Stop();
                }

                // Notify subscribers that border is no longer valid
                WindowInvalidated?.Invoke(this, windowToValidate);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WindowValidator. Error during border validation.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop(); // This will set flags and wait

        lock (_lock)
        {
            _validationTimer?.Dispose();
            _validationTimer = null;
            _disposed = true;
        }
    }
}