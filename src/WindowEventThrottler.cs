using ClunkyBorders.Common;
using System.Threading;

namespace ClunkyBorders;

/// <summary>
/// Throttles rapid consecutive window events to prevent visual artifacts during mouse drag/resize operations.
/// Detects rapid events and delays actions until movement stops.
/// </summary>
internal class WindowEventThrottler : IDisposable
{
    private DateTime _lastEventTime = DateTime.MinValue;
    private Timer? _delayedActionTimer;
    private readonly object _timerLock = new object();
    private Window? _pendingWindow;

    // If events happen within this window, consider it rapid movement (mouse drag)
    private const int RAPID_EVENT_THRESHOLD_MS = 200;
    // Wait this long after last event before executing delayed action
    private const int DELAYED_ACTION_MS = 150;

    private bool _disposed = false;

    /// <summary>
    /// Handles a window event, either executing immediately or delaying based on event timing.
    /// </summary>
    /// <param name="window">The window that triggered the event</param>
    /// <param name="immediateAction">Action to execute immediately for non-rapid events (receives Window)</param>
    /// <param name="rapidEventAction">Action to execute during rapid events (e.g., hide)</param>
    /// <param name="delayedAction">Action to execute after rapid events stop (receives Window)</param>
    public void HandleWindowEvent(
        Window window,
        Action<Window> immediateAction,
        Action rapidEventAction,
        Action<Window> delayedAction)
    {
        // Detect rapid consecutive events (mouse drag/resize in progress)
        var now = DateTime.UtcNow;
        var timeSinceLastEvent = (now - _lastEventTime).TotalMilliseconds;
        _lastEventTime = now;

        if (timeSinceLastEvent < RAPID_EVENT_THRESHOLD_MS && timeSinceLastEvent > 0)
        {
            // Rapid events detected - likely mouse drag/resize in progress
            // Execute rapid event action (e.g., hide) and schedule delayed action
            Logger.Debug($"WindowEventThrottler. Rapid event detected ({timeSinceLastEvent:F0}ms since last). Executing rapid event action and scheduling delayed action.");

            rapidEventAction();

            // Cancel any pending timer and schedule a new one
            lock (_timerLock)
            {
                _delayedActionTimer?.Dispose();
                _pendingWindow = window;

                _delayedActionTimer = new Timer(_ =>
                {
                    try
                    {
                        Window? windowForDelayedAction;
                        lock (_timerLock)
                        {
                            windowForDelayedAction = _pendingWindow;
                            _pendingWindow = null;
                        }

                        if (windowForDelayedAction != null && windowForDelayedAction.IsValidForBorder())
                        {
                            Logger.Debug($"WindowEventThrottler. Delayed action timer fired. Executing delayed action for {windowForDelayedAction.ClassName}");
                            delayedAction(windowForDelayedAction);
                        }
                        else
                        {
                            Logger.Debug($"WindowEventThrottler. Delayed action timer fired but window no longer valid. Skipping.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"WindowEventThrottler. Error in delayed action timer.", ex);
                    }
                }, null, DELAYED_ACTION_MS, Timeout.Infinite);
            }
        }
        else
        {
            // Not a rapid event - execute immediate action
            Logger.Debug($"WindowEventThrottler. Executing immediate action ({timeSinceLastEvent:F0}ms since last event).");

            // Cancel any pending delayed action
            CancelPending();

            immediateAction(window);
        }
    }

    /// <summary>
    /// Cancels any pending delayed action.
    /// </summary>
    public void CancelPending()
    {
        lock (_timerLock)
        {
            _delayedActionTimer?.Dispose();
            _delayedActionTimer = null;
            _pendingWindow = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            lock (_timerLock)
            {
                _delayedActionTimer?.Dispose();
                _delayedActionTimer = null;
            }
        }

        _disposed = true;
    }

    ~WindowEventThrottler()
    {
        Dispose(false);
    }
}
