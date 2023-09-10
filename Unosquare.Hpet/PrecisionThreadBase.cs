using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unosquare.Hpet;

/// <summary>
/// Represents a mostly drop-in replacement for a background <see cref="Thread"/>
/// which executes cycles on monotonic, high precision intervals.
/// </summary>
public abstract class PrecisionThreadBase : IDisposable
{
    private long IsDisposed;
    private readonly CancellationTokenSource TokenSource = new();

    public PrecisionThreadBase(TimeSpan interval)
    {
        Interval = interval;
        WorkerThread = new(WorkerThreadLoop)
        {
            IsBackground = true
        };
    }

    protected Thread WorkerThread { get; }

    public virtual void Start() => WorkerThread.Start();

    protected abstract void ExecuteCycle(PrecisionTickEventArgs tickEvent);

    private void WorkerThreadLoop()
    {
        const int IntervalSampleThreshold = 10;

        var eventDurationsCapacity = Convert.ToInt32(Math.Max(IntervalSampleThreshold, 1d / Interval.TotalSeconds));
        var eventDurations = new Queue<long>(eventDurationsCapacity);

        var eventState = new PrecisionTickEventArgs();
        var nextDelay = Interval;
        var naturalStartTimestamp = default(long);
        var discreteElapsed = TimeSpan.Zero;
        var intervalElapsed = TimeSpan.Zero;
        var naturalDriftOffset = TimeSpan.Zero;
        var averageDriftOffset = TimeSpan.Zero;
        

        eventState.TickNumber = 1;
        eventState.Interval = TimeSpan.Zero;

        var tickStartTimestamp = Stopwatch.GetTimestamp();
        var previousTickTimestamp = default(long);

        while (Interlocked.Read(ref IsDisposed) <= 0)
        {
            // Invoke the user action with the current state
            ExecuteCycle(eventState.Clone());

            // Introduce a delay
            if (GetElapsedTime(tickStartTimestamp).Ticks < nextDelay.Ticks)
            {
                DelayProvider.Delay(
                    TimeSpan.FromTicks(nextDelay.Ticks - GetElapsedTime(tickStartTimestamp).Ticks),
                    TokenSource.Token);
            }

            // start measuring the time interval which includes updating the state for the next tick event
            // and computing event statistics for next cycle.
            previousTickTimestamp = ExchangeTimestamp(ref tickStartTimestamp, out intervalElapsed);

            // Compute an initial estimated delay for the next cycle
            nextDelay = TimeSpan.FromTicks(Interval.Ticks - (intervalElapsed.Ticks - nextDelay.Ticks));

            // compute actual interval elapsed time taking into account drifting due to addition of
            // discrete events not adding up to the natural time elapsed
            naturalDriftOffset = TimeSpan.FromTicks((eventState.NaturalElapsed.Ticks - eventState.DiscreteElapsed.Ticks) % Interval.Ticks);
            // naturalDriftOffset = TimeSpan.Zero;
            eventState.Interval = TimeSpan.FromTicks(intervalElapsed.Ticks + naturalDriftOffset.Ticks);

            // Add the interval elapsed to the discrete elapsed
            eventState.DiscreteElapsed = TimeSpan.FromTicks(eventState.DiscreteElapsed.Ticks + eventState.Interval.Ticks);

            if (eventState.TickNumber <= 1)
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
            eventDurations.Enqueue(eventState.Interval.Ticks);

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

            eventState.TickNumber += (1 + eventState.MissedEventCount);
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

    public TimeSpan Interval { get; }

    /// <summary>
    /// Disposes internal unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="alsoManaged"></param>
    protected virtual void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref IsDisposed) > 1)
            return;

        TokenSource.Cancel();

        if (alsoManaged)
        {
            // TODO: dispose managed state (managed objects)
            TokenSource.Dispose();
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
