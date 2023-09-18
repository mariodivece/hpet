using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unosquare.Hpet.Infrastructure;

/// <summary>
/// Represents a structure that updates a <see cref="PrecisionLoop"/> worker
/// timing information and generates a <see cref="PrecisionCycleEventArgs"/>
/// </summary>
internal record struct LoopState
{
    private readonly Queue<TimeExtent> EventDurations;
    private readonly int EventDurationsCapacity;
    private readonly int IntervalSampleThreshold;
    private readonly PrecisionLoop Loop;
    private readonly PrecisionCycleEventArgs EventState;

    public LoopState(PrecisionLoop loop)
    {
        // capture the initial timestamp
        CurrentTickTimestamp = GetTimestamp();

        Loop = loop;

        // Initialize state variables
        Interval = Loop.Interval;
        EventState = new(interval: Interval, eventIndex: 0, intervalElapsed: TimeExtent.Zero);
        NextDelay = Interval;

        // Compute event duration sample count and instantiate the queue.
        IntervalSampleThreshold = 10; // Math.Max(2, EventDurationsCapacity / 2);
        EventDurationsCapacity = Convert.ToInt32(Math.Max(IntervalSampleThreshold, 1d / Interval.Seconds));
        EventDurations = new Queue<TimeExtent>(EventDurationsCapacity);
    }

    public readonly TimeExtent PendingCycleTime => NextDelay - TimeExtent.FromElapsed(CurrentTickTimestamp);

    public TimeExtent Interval;

    public TimeExtent NextDelay;

    public long NaturalStartTimestamp;

    public TimeExtent IntervalElapsed;

    public TimeExtent NaturalDriftOffset;

    public TimeExtent AverageDriftOffset;

    public Exception? ExitException;

    public long PreviousTickTimestamp;

    public long CurrentTickTimestamp;

    /// <summary>
    /// Provides a consistent timestamp for time measurement purposes.
    /// </summary>
    /// <returns>An internal, incremental timestamp, regarldes of the system date and time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Creates a copy of of the internal <see cref="EventState"/>
    /// for passing on as arguments to either callbacks or events.
    /// </summary>
    /// <returns>A copy of the internal <see cref="EventState"/></returns>
    public readonly PrecisionCycleEventArgs Snapshot() => EventState.Clone();

    /// <summary>
    /// Automatically updates the <see cref="EventState"/> and the internal
    /// timing information of this state tracker.
    /// </summary>
    public void Update()
    {
        // start measuring the time interval which includes updating the state for the next tick event
        // and computing event statistics for next cycle.
        PreviousTickTimestamp = CurrentTickTimestamp;
        IntervalElapsed = CurrentTickTimestamp == default ? TimeExtent.Zero : TimeExtent.FromElapsed(CurrentTickTimestamp);
        CurrentTickTimestamp = GetTimestamp();

        // Cature any updates to the interval from the parent loop.
        Interval = Loop.Interval;
        EventState.Interval = Interval;

        // compute actual interval elapsed time taking into account drifting due to addition of
        // discrete events not adding up to the natural time elapsed
        NaturalDriftOffset = (EventState.NaturalElapsed - EventState.DiscreteElapsed) % Interval;
        IntervalElapsed += NaturalDriftOffset;

        // Compute an initial estimated delay for the next cycle
        NextDelay = Interval - (IntervalElapsed - NextDelay);

        // Add the interval elapsed to the discrete elapsed
        EventState.DiscreteElapsed += IntervalElapsed;

        if (EventState.EventIndex <= 0)
        {
            // on the first tick, start counting the natural time elapsed
            NaturalStartTimestamp = PreviousTickTimestamp;
            EventState.NaturalElapsed = EventState.DiscreteElapsed;
        }
        else
        {
            // Update the natural elapsed time
            EventState.NaturalElapsed = TimeExtent.FromElapsed(NaturalStartTimestamp);
        }

        // Limit the amount of samples.
        if (EventDurations.Count >= EventDurationsCapacity)
            _ = EventDurations.Dequeue();

        // Push a sample to the analysis set.
        EventState.IntervalElapsed = IntervalElapsed;
        EventDurations.Enqueue(IntervalElapsed);

        // Compute the average.
        EventState.IntervalAverage = EventDurations.Average(c => c.Seconds);

        // Compute the frequency
        EventState.Frequency = EventState.IntervalAverage != TimeExtent.Zero
            ? 1d / EventState.IntervalAverage
            : 0;

        // Jitter is the standard deviation.
        var intervalTicks = Interval.Seconds;
        EventState.IntervalJitter = 
            Math.Sqrt(EventDurations.Sum(x => Math.Pow(x - intervalTicks, 2)) / EventDurations.Count);

        // compute drifting to account for average event duration
        if (EventDurations.Count >= IntervalSampleThreshold / 2)
        {
            AverageDriftOffset = (EventState.IntervalAverage - Interval) % Interval;
            NextDelay -= AverageDriftOffset;
        }

        // compute missed events.
        if (NextDelay <= TimeExtent.Zero)
        {
            EventState.MissedEventCount = 1 + Convert.ToInt32(-NextDelay / Interval);
            EventState.TotalMissedEventCount += EventState.MissedEventCount;
            NextDelay = Interval;
        }
        else
        {
            EventState.MissedEventCount = 0;
        }

        EventState.EventIndex += 1 + EventState.MissedEventCount;
    }
}
