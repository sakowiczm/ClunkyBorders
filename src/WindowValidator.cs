namespace ClunkyBorders;

/// <summary>
/// Not every window can get border - even if in Admin Mode - 
/// just hide border if border is not present on active window.
/// </summary>
internal class WindowValidator : IDisposable
{
    public event EventHandler<Window>? WindowInvalidated;

    private readonly int _validationInterval;
    private Window? _currentWindow = null;
    private Timer? _validationTimer;
    private bool _disposed = false;

    public WindowValidator(int validationInterval = 250)
    {
        _validationInterval = validationInterval;
        _validationTimer = new Timer(ValidateBorderStillValid, null, Timeout.Infinite, Timeout.Infinite);

        Logger.Info($"WindowValidator. Initialized with validation interval: {_validationInterval}ms");
    }

    public void Start(Window window)
    {
        _currentWindow = window;
        _validationTimer?.Change(_validationInterval, _validationInterval);
        Logger.Debug($"WindowValidator. Started monitoring window: {window.ClassName}");
    }

    public void Stop()
    {
        _validationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _currentWindow = null;
        Logger.Debug("WindowValidator. Stopped monitoring");
    }

    private void ValidateBorderStillValid(object? state)
    {
        try
        {
            // If no window is being monitored, nothing to validate
            if (_currentWindow == null)
            {
                return;
            }

            // Check if the bordered window is still the foreground window
            if (!_currentWindow.IsForegroundWindow())
            {
                Logger.Info($"WindowValidator. Window {_currentWindow.ClassName} is no longer foreground.");

                var window = _currentWindow;
                Stop();

                // Notify subscribers that border is no longer valid
                WindowInvalidated?.Invoke(this, window);
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

        Stop();
        _validationTimer?.Dispose();
        _validationTimer = null;

        _disposed = true;
    }
}