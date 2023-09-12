using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unosquare.Hpet;

internal record struct LoopState
{
    private readonly Queue<long> EventDurations;
    private readonly int EventDurationsCapacity;
    private readonly int IntervalSampleThreshold;
    private readonly PrecisionLoopBase Loop;
    private readonly PrecisionTickEventArgs EventState;

    public LoopState(PrecisionLoopBase loop)
    {
        // capture the initial timestamp
        CurrentTickTimestamp = GetTimestamp();

        Loop = loop;

        // Initialize state variables
        Interval = Loop.Interval;
        EventState = new(interval: Interval, tickEventNumber: 1, intervalElapsed: TimeSpan.Zero);
        NextDelay = Interval;

        // Compute event duration sample count and instantiate the queue.
        EventDurationsCapacity = Convert.ToInt32(Math.Max(IntervalSampleThreshold, 1d / Interval.TotalSeconds));
        EventDurations = new Queue<long>(EventDurationsCapacity);
        IntervalSampleThreshold = Math.Max(2, EventDurationsCapacity / 2);
    }

    public readonly bool HasCycleIntervalElapsed =>
        GetElapsedTime(CurrentTickTimestamp).Ticks >= NextDelay.Ticks;

    public readonly TimeSpan PendingCycleTimeSpan =>
        TimeSpan.FromTicks(NextDelay.Ticks - GetElapsedTime(CurrentTickTimestamp).Ticks);

    public TimeSpan Interval;

    public TimeSpan NextDelay;

    public long NaturalStartTimestamp;

    public TimeSpan DiscreteElapsed;

    public TimeSpan IntervalElapsed;

    public TimeSpan NaturalDriftOffset;

    public TimeSpan AverageDriftOffset;

    public Exception? ExitException;

    public long PreviousTickTimestamp;

    public long CurrentTickTimestamp;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetTimestamp() => Stopwatch.GetTimestamp();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan GetElapsedTime(long startingTimestamp) => Stopwatch.GetElapsedTime(startingTimestamp);

    public PrecisionTickEventArgs Snapshot() => EventState.Clone();

    public void Update()
    {
        // start measuring the time interval which includes updating the state for the next tick event
        // and computing event statistics for next cycle.
        PreviousTickTimestamp = CurrentTickTimestamp;
        IntervalElapsed = CurrentTickTimestamp == default ? TimeSpan.Zero : GetElapsedTime(CurrentTickTimestamp);
        CurrentTickTimestamp = GetTimestamp();

        // Cature any updates to the interval from the parent loop.
        Interval = Loop.Interval;
        EventState.Interval = Interval;

        // compute actual interval elapsed time taking into account drifting due to addition of
        // discrete events not adding up to the natural time elapsed
        NaturalDriftOffset = TimeSpan.FromTicks((EventState.NaturalElapsed.Ticks - EventState.DiscreteElapsed.Ticks) % Interval.Ticks);
        IntervalElapsed = TimeSpan.FromTicks(IntervalElapsed.Ticks + NaturalDriftOffset.Ticks);

        // Compute an initial estimated delay for the next cycle
        NextDelay = TimeSpan.FromTicks(Interval.Ticks - (IntervalElapsed.Ticks - NextDelay.Ticks));

        // Add the interval elapsed to the discrete elapsed
        EventState.DiscreteElapsed = TimeSpan.FromTicks(EventState.DiscreteElapsed.Ticks + IntervalElapsed.Ticks);

        if (EventState.TickEventNumber <= 1)
        {
            // on the first tick, start counting the natural time elapsed
            NaturalStartTimestamp = PreviousTickTimestamp;
            EventState.NaturalElapsed = EventState.DiscreteElapsed;
        }
        else
        {
            // Update the natural elapsed time
            EventState.NaturalElapsed = GetElapsedTime(NaturalStartTimestamp);
        }

        // Limit the amount of samples.
        if (EventDurations.Count >= EventDurationsCapacity)
            _ = EventDurations.Dequeue();

        // Push a sample to the analysis set.
        EventState.IntervalElapsed = IntervalElapsed;
        EventDurations.Enqueue(IntervalElapsed.Ticks);

        // Compute the average.
        EventState.IntervalAverage = TimeSpan.FromTicks(
            Convert.ToInt64(EventDurations.Average()));

        // Jitter is the standard deviation.
        var intervalTicks = Interval.Ticks;
        EventState.IntervalJitter = TimeSpan.FromTicks(
            Convert.ToInt64(Math.Sqrt(EventDurations.Sum(x => Math.Pow(x - intervalTicks, 2)) / EventDurations.Count)));

        // compute drifting to account for average event duration
        if (EventDurations.Count >= IntervalSampleThreshold)
        {
            AverageDriftOffset = TimeSpan.FromTicks((EventState.IntervalAverage.Ticks - Interval.Ticks) % Interval.Ticks);
            NextDelay = TimeSpan.FromTicks(NextDelay.Ticks - AverageDriftOffset.Ticks);
        }

        // compute missed events.
        if (NextDelay.Ticks <= 0)
        {
            EventState.MissedEventCount = 1 + Convert.ToInt32(-NextDelay.Ticks / Interval.Ticks);
            NextDelay = Interval;
        }
        else
        {
            EventState.MissedEventCount = 0;
        }

        EventState.TickEventNumber += (1 + EventState.MissedEventCount);
    }

}
