namespace Unosquare.Hpet.Infrastructure;

/// <summary>
/// Defines the members of a precision loop which runs a set of scheduled cycles
/// in a monotonic, precise and accurate time intervals.
/// </summary>
public interface IPrecisionLoop : IDisposable
{
    /// <summary>
    /// Gets the requested interval at which cycles are to be monotonically executed.
    /// </summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Starts the worker loop and begins executing cycles. This method does not block and
    /// it is guaranteed to be allowed to be called only once.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when <see cref="IDisposable.Dispose()"/> has been called before.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the <see cref="Start"/> method has already been called.</exception>
    void Start();

    /// <summary>
    /// Provides an awaitable task to wait for thread loop termination
    /// and guaranteeing no more cycles will be executed.
    /// Do not await this method in the same thread where cycle work is performed.
    /// </summary>
    /// <returns>An awaitable task.</returns>
    Task WaitForExitAsync();
}
