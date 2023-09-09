using System.Diagnostics;
using Unosquare.Hpet.TickSources;

namespace Unosquare.Hpet;

/// <summary>
/// Emulates a monotonic, High Precision Event Timer (HPET) while attempting
/// to avoid CPU busy waits as much as possible and accounting for
/// time drifts between <see cref="Ticked"/> events.
/// </summary>
public partial class PrecisionTimer : IDisposable
{
    // TODO: https://stackoverflow.com/questions/1416139/how-to-get-timestamp-of-tick-precision-in-net-c
    // TODO: Add a way to get or set TimeSlice timeBeginPeriod and timeEndPeriod and output a disposable
    // TODO: Encapsulate GetTimestamp with calibrated precise as FileTime wwith stopwatch
    // TODO: Stopwatch will drift about 5 ms per hour.

    /// <summary>
    /// Subscribe to this event to execute code when the <see cref="PrecisionTimer"/> ticks.
    /// </summary>
    public event EventHandler<PrecisionTickEventArgs>? Ticked;

    private readonly WinMMTickSource TickSource;
    private readonly TimerTickCallback TickSourceTickHandler;
    private readonly Queue<long> EventDurations = new(128);

    private long IsDisposed;
    private long StartTimestamp;
    private long TickEventNumber;
    private long DiscreteElapsedTicks;
    
    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionTimer"/> and starts ticking inmmediately.
    /// </summary>
    /// <param name="interval">The ticking interval expressed as a <see cref="TimeSpan"/>. The minimum interval is 1 millisecond.</param>
    public PrecisionTimer(TimeSpan interval)
    {
        _ = Interlocked.Exchange(ref StartTimestamp, Stopwatch.GetTimestamp());
        Interval = interval.TotalMilliseconds <= 1 ? TimeSpan.FromMilliseconds(1) : interval;
        TickSourceTickHandler = new(HandleTickSourceTickEvent);
        TickSource = WinMMTickSource.CreateInstance(interval, TickSourceTickHandler);
    }

    /// <summary>
    /// Gets the desired time interval between events.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Handles the ticking source event and fires the event to be handled by user code.
    /// </summary>
    /// <param name="eventElapsed">The amount of time elapsed between the lasta nd the current event.</param>
    /// <param name="skipped">The number of events skipped due to either late ticks or handlers that took longer than one or more intervals.</param>
    private void HandleTickSourceTickEvent(TimeSpan eventElapsed, int skipped)
    {
        var currentInterval = Interval;
        var currentTickEventNumber = Interlocked.Increment(ref TickEventNumber);
        var currentElapsedTicks = Interlocked.Add(ref DiscreteElapsedTicks, eventElapsed.Ticks);
        var tickHandler = Ticked;

        if (EventDurations.Count > 100)
            _ = EventDurations.Dequeue();

        if (currentTickEventNumber > 1)
            EventDurations.Enqueue(eventElapsed.Ticks);

        if (tickHandler is null)
            return;

        var discreteElapsed = TimeSpan.FromTicks(currentElapsedTicks);
        var elapsedAverage = TimeSpan.FromTicks(EventDurations.Count > 0 ? Convert.ToInt64(EventDurations.Average()) : currentInterval.Ticks);
        var elapsedStd = TimeSpan.FromTicks(EventDurations.Count > 0
            ? Convert.ToInt64(Math.Sqrt(EventDurations.Sum(x => Math.Pow(x - currentInterval.Ticks, 2)) / EventDurations.Count))
            : 0);
        var naturalElapsed = Stopwatch.GetElapsedTime(StartTimestamp);

        tickHandler.Invoke(this, new(TickEventNumber, skipped, currentInterval, elapsedAverage, elapsedStd, naturalElapsed, discreteElapsed));

#if DEBUG
        Console.CursorLeft = 0;
        Console.CursorTop = 0;

        Console.WriteLine($"""
                Period:   {currentInterval.TotalMilliseconds,16:N4} ms.
                Number:   {TickEventNumber,16}
                Elapsed:  {eventElapsed.TotalMilliseconds,16:N4} ms.
                Average:  {elapsedAverage.TotalMilliseconds,16:N4} ms.
                Jitter:   {elapsedStd.TotalMilliseconds,16:N4} ms.
                Skipped:  {skipped,16} cycles
                Discrete: {discreteElapsed.TotalMilliseconds,16:N4} ms.
                Natural:  {naturalElapsed.TotalMilliseconds,16:N4} ms.
                """);
#endif
    }

    /// <summary>
    /// Disposes resources internal to this instance.
    /// </summary>
    /// <param name="alsoManaged">Wehn set to true, disposes managed resources.</param>
    protected virtual void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref IsDisposed) > 1)
            return;

        if (alsoManaged)
        {
            TickSource.Dispose();
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