using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unosquare.Hpet;

/// <summary>
/// Represents a background <see cref="Thread"/>
/// which executes cycles on monotonic, precise, and accurate intervals.
/// Override the <see cref="OnWorkerCycle(PrecisionTickEventArgs)"/> to perform work for a single cycle.
/// Call the <see cref="Start"/> method to begin executing cycles.
/// Call the <see cref="Dispose()"/> method to request the background worker to stop executing cycles.
/// Override the <see cref="OnWorkerFinished(Exception?)"/> to get notified when no more cycles will be executed.
/// </summary>
public abstract class PrecisionThreadBase : IDisposable
{
    private long m_IsDisposed;
    private WeakReference<CancellationTokenSource>? TokenSourceReference;
    private readonly object SyncLock = new();

    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionThreadBase"/> class.
    /// </summary>
    /// <param name="interval">The desired cycle execution interval. Must be a positive value.</param>
    /// <param name="precisionOption">The delay precision strategy to employ.</param>
    protected PrecisionThreadBase(TimeSpan interval, DelayPrecision precisionOption)
    {
        Interval = interval.Ticks <= 0 ? TimeSpan.FromMilliseconds(1) : interval;
        WorkerThread = new(WorkerThreadLoop)
        {
            IsBackground = true
        };
        PrecisionOption = precisionOption;
    }

    /// <summary>
    /// Gets the requested interval at which cycles are to be monotonically executed.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Gets the delay precision strategy to employ.
    /// </summary>
    protected DelayPrecision PrecisionOption { get; }

    /// <summary>
    /// Gets a value indicating whether the <see cref="Dispose()"/> method has been called
    /// and the running loop is about to exit or already has.
    /// </summary>
    protected bool IsDisposeRequested => Interlocked.Read(ref m_IsDisposed) > 0;

    /// <summary>
    /// Provides access to the underlying background thread.
    /// </summary>
    protected Thread WorkerThread { get; }

    /// <summary>
    /// Starts the worker thread loop and begins executing cycles.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when <see cref="Dispose()"/> has been called before.</exception>
    public virtual void Start()
    {
        if (IsDisposeRequested)
            throw new ObjectDisposedException(nameof(PrecisionThreadBase));

        WorkerThread.Start();
    }

    /// <summary>
    /// Implement this method to perform the actions needed for a single cycle execution.
    /// Ideally, you should ensure the execution of the cycle does not take longer than <see cref="Interval"/>.
    /// </summary>
    /// <param name="tickEvent">Provides timing information on the current cycle.</param>
    protected abstract void OnWorkerCycle(PrecisionTickEventArgs tickEvent);

    /// <summary>
    /// Called when <see cref="OnWorkerCycle(PrecisionTickEventArgs)"/> throws an unhandled exception.
    /// Override this method to handle the exception and decide whether or not execution can continue.
    /// By default this method will simply ignore the exception and signal the worker thread to exit.
    /// </summary>
    /// <param name="ex">The unhandled exception that was thrown.</param>
    /// <param name="exitWorker">A value to signal the worker thread to exit the cycle execution loop.</param>
    protected virtual void OnWorkerExeption(Exception ex, out bool exitWorker)
    {
        exitWorker = true;
    }

    /// <summary>
    /// Called when the worker thread can guarantee no more <see cref="OnWorkerCycle(PrecisionTickEventArgs)"/>
    /// methods calls will be made and right before the <see cref="Dispose()"/> method is automatically called.
    /// </summary>
    /// <param name="exitException">When set, contains the exception that caused the worker to exit the cycle execution loop.</param>
    protected virtual void OnWorkerFinished(Exception? exitException)
    {
        // placeholder
    }

    /// <summary>
    /// Continuously and monotonically calls <see cref="OnWorkerCycle(PrecisionTickEventArgs)"/>
    /// at the specified <see cref="Interval"/>.
    /// </summary>
    private void WorkerThreadLoop()
    {
        const int IntervalSampleThreshold = 10;

        // Compute event duration sample count and instantiate the queue.
        var eventDurationsCapacity = Convert.ToInt32(Math.Max(IntervalSampleThreshold, 1d / Interval.TotalSeconds));
        var eventDurations = new Queue<long>(eventDurationsCapacity);

        // Setup state and looping variables.
        var eventState = new PrecisionTickEventArgs(interval: Interval);
        var nextDelay = Interval;
        var naturalStartTimestamp = default(long);
        var discreteElapsed = TimeSpan.Zero;
        var intervalElapsed = TimeSpan.Zero;
        var naturalDriftOffset = TimeSpan.Zero;
        var averageDriftOffset = TimeSpan.Zero;

        eventState.TickEventNumber = 1;
        eventState.IntervalElapsed = TimeSpan.Zero;

        Exception? exitException = null;

        // Cature a reference to the CTS so that it can be signalled
        // from a different method.
        using var tokenSource = new CancellationTokenSource();
        TokenSourceReference = new(tokenSource);

        var previousTickTimestamp = default(long);
        var tickStartTimestamp = Stopwatch.GetTimestamp();

        while (!IsDisposeRequested)
        {
            // Invoke the user action with the current state
            try
            {
                OnWorkerCycle(eventState.Clone());
            }
            catch (Exception ex)
            {
                OnWorkerExeption(ex, out var exitWorker);
                if (exitWorker)
                {
                    exitException = ex;
                    break;
                }
            }

            // Capture the cancellation token with thread safety
            // Introduce a delay
            if (GetElapsedTime(tickStartTimestamp).Ticks < nextDelay.Ticks)
            {
                DelayProvider.Delay(
                    TimeSpan.FromTicks(nextDelay.Ticks - GetElapsedTime(tickStartTimestamp).Ticks),
                    PrecisionOption,
                    tokenSource.Token);
            }

            // start measuring the time interval which includes updating the state for the next tick event
            // and computing event statistics for next cycle.
            previousTickTimestamp = ExchangeTimestamp(ref tickStartTimestamp, out intervalElapsed);

            // Compute an initial estimated delay for the next cycle
            nextDelay = TimeSpan.FromTicks(Interval.Ticks - (intervalElapsed.Ticks - nextDelay.Ticks));

            // compute actual interval elapsed time taking into account drifting due to addition of
            // discrete events not adding up to the natural time elapsed
            naturalDriftOffset = TimeSpan.FromTicks((eventState.NaturalElapsed.Ticks - eventState.DiscreteElapsed.Ticks) % Interval.Ticks);
            eventState.IntervalElapsed = TimeSpan.FromTicks(intervalElapsed.Ticks + naturalDriftOffset.Ticks);

            // Add the interval elapsed to the discrete elapsed
            eventState.DiscreteElapsed = TimeSpan.FromTicks(eventState.DiscreteElapsed.Ticks + eventState.IntervalElapsed.Ticks);

            if (eventState.TickEventNumber <= 1)
            {
                // on the first tick, start counting the natural time elapsed
                naturalStartTimestamp = previousTickTimestamp;
                eventState.NaturalElapsed = eventState.DiscreteElapsed;
            }
            else
            {
                // Update the natural elapsed time
                eventState.NaturalElapsed = GetElapsedTime(naturalStartTimestamp);
            }

            // Limit the amount of samples.
            if (eventDurations.Count >= eventDurationsCapacity)
                _ = eventDurations.Dequeue();

            // Push a sample to the analysis set.
            eventDurations.Enqueue(eventState.IntervalElapsed.Ticks);

            // Compute the average.
            eventState.IntervalAverage = TimeSpan.FromTicks(
                Convert.ToInt64(eventDurations.Average()));

            // Jitter is the standard deviation.
            eventState.IntervalJitter = TimeSpan.FromTicks(
                Convert.ToInt64(Math.Sqrt(eventDurations.Sum(x => Math.Pow(x - Interval.Ticks, 2)) / eventDurations.Count)));

            // compute drifting to account for average event duration
            if (eventDurations.Count >= IntervalSampleThreshold)
            {
                averageDriftOffset = TimeSpan.FromTicks((eventState.IntervalAverage.Ticks - Interval.Ticks) % Interval.Ticks);
                nextDelay = TimeSpan.FromTicks(nextDelay.Ticks - averageDriftOffset.Ticks);
            }

            // compute missed events.
            if (nextDelay.Ticks <= 0)
            {
                eventState.MissedEventCount = 1 + Convert.ToInt32(-nextDelay.Ticks / Interval.Ticks);
                nextDelay = TimeSpan.FromTicks(-nextDelay.Ticks % Interval.Ticks);
            }
            else
            {
                eventState.MissedEventCount = 0;
            }

            eventState.TickEventNumber += (1 + eventState.MissedEventCount);
        }

        // Notify the worker finished and always dispose immediately.
        try
        {
            OnWorkerFinished(exitException);
        }
        finally
        {
            Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan GetElapsedTime(long startingTimestamp) => Stopwatch.GetElapsedTime(startingTimestamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ExchangeTimestamp(ref long startingTimestamp, out TimeSpan lastElapsed)
    {
        var previousValue = startingTimestamp;
        lastElapsed = startingTimestamp == default ? TimeSpan.Zero : GetElapsedTime(startingTimestamp);
        startingTimestamp = Stopwatch.GetTimestamp();
        return previousValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalCancellation()
    {
        lock (SyncLock)
        {
            if (TokenSourceReference is null || !TokenSourceReference.TryGetTarget(out var tokenSource))
                return;

            TokenSourceReference = null;
            
            try
            {
                tokenSource.Cancel();
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Disposes internal unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="alsoManaged"></param>
    protected virtual void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref m_IsDisposed) > 1)
            return;

        SignalCancellation();

        if (alsoManaged)
        {
            // TODO: dispose managed state (managed objects)
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }
}
