namespace Unosquare.Hpet;

/// <summary>
/// Provides an alternative implementation of the <see cref="PrecisionThread"/> class
/// that uses <see cref="Task"/> infrastructure for asynchronous operations.
/// </summary>
public class PrecisionTask : PrecisionLoop
{
    private readonly TaskCompletionSource WorkerExitTaskSource;
    private readonly Func<PrecisionCycleEventArgs, CancellationToken, ValueTask> CycleAction;

    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionTask"/> class.
    /// </summary>
    /// <param name="cycleAction">The action that returns an awaitable task.</param>
    /// <param name="interval">The configured interval.</param>
    /// <param name="precisionOption">The precision strategy.</param>
    public PrecisionTask(Func<PrecisionCycleEventArgs, CancellationToken, ValueTask> cycleAction, TimeSpan interval, DelayPrecision precisionOption)
        : base(interval, precisionOption)
    {
        WorkerExitTaskSource = new(this);
        CycleAction = cycleAction;
    }

    /// <summary>
    /// Provides access to the underlying <see cref="Task"/>.
    /// </summary>
    protected Task? WorkerTask { get; private set; }

    /// <inheridoc />
    protected override void StartWorker()
    {
        WorkerTask = Task.Factory.StartNew(
                    RunWorkerLoopAsync,
                    this,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
    }

    /// <summary>
    /// Implement this method to perform the actions needed for a single cycle execution.
    /// Ideally, you should ensure the execution of the cycle does not take longer than <see cref="PrecisionLoop.Interval"/>.
    /// </summary>
    /// <param name="cycleEvent">Provides timing information on the current cycle.</param>
    /// <param name="ct">The cancellation token.</param>
    protected virtual ValueTask DoCycleWorkAsync(PrecisionCycleEventArgs cycleEvent, CancellationToken ct) => CycleAction(cycleEvent, ct);

    /// <summary>
    /// Called when <see cref="DoCycleWorkAsync"/> throws an unhandled exception.
    /// Override this method to handle the exception and decide whether or not execution can continue.
    /// By default this method will simply ignore the exception and signal the worker thread to exit.
    /// </summary>
    /// <param name="ex">The unhandled exception that was thrown.</param>
    /// <param name="exitWorker">A value to signal the worker thread to exit the cycle execution loop.</param>
    protected virtual ValueTask OnCycleExeptionAsync(Exception ex, out bool exitWorker)
    {
        exitWorker = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called when the worker thread can guarantee no more <see cref="DoCycleWorkAsync"/>
    /// methods calls will be made and right before the <see cref="PrecisionLoop.Dispose()"/> method is automatically called.
    /// </summary>
    /// <param name="exitException">When set, contains the exception that caused the worker to exit the cycle execution loop.</param>
    protected virtual ValueTask OnWorkerFinishedAsync(Exception? exitException)
    {
        if (exitException is not null)
            WorkerExitTaskSource.TrySetException(exitException);
        else
            WorkerExitTaskSource.TrySetResult();

        return ValueTask.CompletedTask;
    }

    private async ValueTask RunWorkerLoopAsync(object? state)
    {
#pragma warning disable CA1031
        // create a loop state object to keep track of cycles and timing
        var s = new LoopState(this);

        // Cature a reference to the CTS so that it can be signalled
        // from a different method.
        using var tokenSource = CaptureTokenSource();

        while (!IsCancellationRequested)
        {
            // Invoke the user action with the current state
            try
            {
                await DoCycleWorkAsync(s.Snapshot(), tokenSource.Token).ConfigureAwait(false);

                // Introduce a delay
                if (!s.HasCycleIntervalElapsed)
                {
                    await DelayProvider.DelayAsync(
                        s.PendingCycleTimeSpan,
                        PrecisionOption,
                        tokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await OnCycleExeptionAsync(ex, out var exitWorker).ConfigureAwait(false);
                if (exitWorker)
                {
                    s.ExitException = ex;
                    break;
                }
            }
            finally
            {
                s.Update();
            }            
        }

        // Notify the worker finished and always dispose immediately.
        try
        {
            await OnWorkerFinishedAsync(s.ExitException).ConfigureAwait(false);
        }
        finally
        {
            Dispose();
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public override Task WaitForExitAsync() => WorkerExitTaskSource.Task;
}
