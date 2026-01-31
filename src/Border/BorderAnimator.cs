namespace ClunkyBorders;

/// <summary>
/// Handles smooth fade-in/fade-out animations for border rendering.
/// Manages animation state, timing, and alpha blending calculations.
/// </summary>
internal sealed class BorderAnimator : IDisposable
{
    private const int ANIMATION_STEPS = 15;
    private CancellationTokenSource? _cancellationTokenSource;
    private byte _currentAlpha = 255;

    private readonly int _durationMs;
    private readonly bool _enabled;

    public byte CurrentAlpha => _currentAlpha;

    public bool IsAnimating => _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;

    public BorderAnimator(bool enabled, int durationMs)
    {
        _enabled = enabled;
        _durationMs = Math.Clamp(durationMs, 50, 1000);
    }

    /// <summary>
    /// Performs fade-in animation from transparent to opaque.
    /// </summary>
    /// <param name="onAlphaChange">
    /// Callback invoked for each alpha value change. 
    /// Parameters: (alpha, isComplete) where alpha is 0-255 and isComplete indicates final frame.
    /// </param>
    public async Task FadeInAsync(Func<byte, bool, Task> onAlphaChange)
    {
        if (!_enabled)
        {
            _currentAlpha = 255;
            await onAlphaChange(255, true);
            return;
        }

        CancelCurrentAnimation();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            var stepDelay = CalculateStepDelay();

            for (int step = 1; step <= ANIMATION_STEPS; step++)
            {
                token.ThrowIfCancellationRequested();

                _currentAlpha = CalculateAlpha(step, ANIMATION_STEPS);
                bool isComplete = step == ANIMATION_STEPS;

                await onAlphaChange(_currentAlpha, isComplete);

                if (!isComplete)
                    await Task.Delay(stepDelay, token);
            }

            // Ensure fully opaque at the end
            _currentAlpha = 255;
        }
        catch (OperationCanceledException)
        {
            // Animation cancelled - this is normal
        }
    }

    /// <summary>
    /// Performs fade-out animation from opaque to transparent.
    /// </summary>
    /// <param name="onAlphaChange">
    /// Callback invoked for each alpha value change.
    /// Parameters: (alpha, isComplete) where alpha is 0-255 and isComplete indicates final frame.
    /// </param>
    public async Task FadeOutAsync(Func<byte, bool, Task> onAlphaChange)
    {
        if (!_enabled)
        {
            _currentAlpha = 0;
            await onAlphaChange(0, true);
            return;
        }

        CancelCurrentAnimation();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            var stepDelay = CalculateStepDelay();

            for (int step = ANIMATION_STEPS - 1; step >= 0; step--)
            {
                token.ThrowIfCancellationRequested();

                _currentAlpha = CalculateAlpha(step, ANIMATION_STEPS);
                bool isComplete = step == 0;

                await onAlphaChange(_currentAlpha, isComplete);

                if (!isComplete)
                    await Task.Delay(stepDelay, token);
            }

            // Ensure fully transparent at the end
            _currentAlpha = 0;
        }
        catch (OperationCanceledException)
        {
            // Animation cancelled - reset to transparent
            _currentAlpha = 0;
            throw; // Re-throw to allow caller to handle immediate hide
        }
    }

    public void CancelCurrentAnimation()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public void Reset()
    {
        CancelCurrentAnimation();
        _currentAlpha = 255;
    }

    /// <summary>
    /// Calculates the delay between animation steps based on duration.
    /// </summary>
    private int CalculateStepDelay() => Math.Max(1, _durationMs / ANIMATION_STEPS);

    /// <summary>
    /// Calculates the alpha value for a given animation step.
    /// </summary>
    /// <param name="step">Current animation step (0-based).</param>
    /// <param name="totalSteps">Total number of animation steps.</param>
    /// <returns>Alpha value (0-255).</returns>
    private static byte CalculateAlpha(int step, int totalSteps) =>
        (byte)(255 * step / totalSteps);

    public void Dispose()
    {
        CancelCurrentAnimation();
    }
}
