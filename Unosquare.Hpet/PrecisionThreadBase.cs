namespace Unosquare.Hpet;

/// <summary>
/// Represents a background <see cref="Thread"/>
/// which executes cycles on monotonic, precise, and accurate intervals.
/// Override the <see cref="DoCycleWork(PrecisionCycleEventArgs)"/> to perform work for a single cycle.
/// Call the <see cref="PrecisionLoop.Start()"/> method to begin executing cycles.
/// Call the <see cref="PrecisionLoop.Dispose()"/> method to request the background worker to stop executing cycles.
/// Override the <see cref="OnWorkerFinished(Exception?)"/> to get notified when no more cycles will be executed.
/// </summary>
public abstract class PrecisionThreadBase : PrecisionLoop
{
    private readonly TaskCompletionSource WorkerExitTaskSource;

    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionThreadBase"/> class.
    /// </summary>
    /// <param name="interval">The desired cycle execution interval. Must be a positive value.</param>
    /// <param name="precisionOption">The delay precision strategy to employ.</param>
    protected PrecisionThreadBase(TimeSpan interval, DelayPrecision precisionOption)
        : base(interval, precisionOption)
    {
        WorkerExitTaskSource = new(this);

        WorkerThread = new(WorkerThreadLoop)
        {
            IsBackground = true
        };
    }

    /// <summary>
    /// Provides access to the underlying background thread.
    /// </summary>
    protected Thread WorkerThread { get; }

    /// <inheritdoc />
    protected override void StartWorker()
    {
        WorkerThread.Start();
    }

    /// <summary>
    /// Implement this method to perform the actions needed for a single cycle execution.
    /// Ideally, you should ensure the execution of the cycle does not take longer than <see cref="PrecisionLoop.Interval"/>.
    /// </summary>
    /// <param name="cycleEvent">Provides timing information on the current cycle.</param>
    protected abstract void DoCycleWork(PrecisionCycleEventArgs cycleEvent);

    /// <summary>
    /// Called when <see cref="DoCycleWork(PrecisionCycleEventArgs)"/> throws an unhandled exception.
    /// Override this method to handle the exception and decide whether or not execution can continue.
    /// By default this method will simply ignore the exception and signal the worker thread to exit.
    /// </summary>
    /// <param name="ex">The unhandled exception that was thrown.</param>
    /// <param name="exitWorker">A value to signal the worker thread to exit the cycle execution loop.</param>
    protected virtual void OnCycleExeption(Exception ex, out bool exitWorker)
    {
        exitWorker = true;
    }

    /// <summary>
    /// Called when the worker thread can guarantee no more <see cref="DoCycleWork(PrecisionCycleEventArgs)"/>
    /// methods calls will be made and right before the <see cref="PrecisionLoop.Dispose()"/> method is automatically called.
    /// </summary>
    /// <param name="exitException">When set, contains the exception that caused the worker to exit the cycle execution loop.</param>
    protected virtual void OnWorkerFinished(Exception? exitException)
    {
        if (exitException is not null)
        {
            WorkerExitTaskSource.TrySetException(exitException);
            return;
        }

        WorkerExitTaskSource.TrySetResult();
    }

    /// <summary>
    /// Continuously and monotonically calls <see cref="DoCycleWork(PrecisionCycleEventArgs)"/>
    /// at the specified <see cref="PrecisionLoop.Interval"/>.
    /// </summary>
    private void WorkerThreadLoop()
    {
        // create a loop state object to keep track of cycles and timing
        var s = new LoopState(this);

        // Cature a reference to the CTS so that it can be signalled
        // from a different method.
        using var tokenSource = CaptureTokenSource();

        while (!IsCancellationRequested)
        {
            // Invoke the user action with the current state
#pragma warning disable CA1031
            try
            {
                DoCycleWork(s.Snapshot());
            }
            catch (Exception ex)
            {
                OnCycleExeption(ex, out var exitWorker);
                if (exitWorker)
                {
                    s.ExitException = ex;
                    break;
                }
            }
#pragma warning restore CA1031

            // Introduce a delay
            if (!s.HasCycleIntervalElapsed)
            {
                DelayProvider.Delay(
                    s.PendingCycleTimeSpan,
                    PrecisionOption,
                    tokenSource.Token);
            }

            s.Update();
        }

        // Notify the worker finished and always dispose immediately.
        try
        {
            OnWorkerFinished(s.ExitException);
        }
        finally
        {
            Dispose();
        }
    }

    /// <summary>
    /// Provides an awaitable task to wait for thread loop termination
    /// and guaranteeing no more cycles will be executed.
    /// </summary>
    /// <returns>An awaitable task.</returns>
    public Task WaitForExitAsync() => WorkerExitTaskSource.Task;
}
