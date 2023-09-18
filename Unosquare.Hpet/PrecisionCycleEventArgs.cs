using Unosquare.Hpet.Infrastructure;

namespace Unosquare.Hpet;

/// <summary>
/// Represents data associated with timing information of a precision loop cycle.
/// This class is compatible with <see cref="EventArgs"/> so that it can be used
/// in event firing and handling.
/// </summary>
public sealed class PrecisionCycleEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionCycleEventArgs"/>
    /// </summary>
    internal PrecisionCycleEventArgs(
        long eventIndex = default,
        int missedEventCount = default,
        long totalMissedEventCount = default,
        TimeExtent interval = default,
        TimeExtent intervalElapsed = default,
        TimeExtent intervalAverage = default,
        double frequency = default,
        TimeExtent intevalJitter = default,
        TimeExtent naturalElapsed = default,
        TimeExtent discreteElapsed = default)
        : base()
    {
        EventIndex = eventIndex;
        MissedEventCount = missedEventCount;
        Interval = interval;
        IntervalElapsed = intervalElapsed;
        IntervalAverage = intervalAverage;
        Frequency = frequency;
        IntervalJitter = intevalJitter;
        NaturalElapsed = naturalElapsed;
        DiscreteElapsed = discreteElapsed;
        TotalMissedEventCount = totalMissedEventCount;
    }

    /// <summary>
    /// Gets the incremental event index starting from 0.
    /// Event indices are not consecutive if timer events are missed.
    /// </summary>
    public long EventIndex { get; internal set; }

    /// <summary>
    /// The number of timer events that were not fired due to excessive time
    /// taken to fire the event internally or synchronously handle the event.
    /// </summary>
    public int MissedEventCount { get; internal set; }

    /// <summary>
    /// The accumulated missed event count of all cycles combined. 
    /// </summary>
    public long TotalMissedEventCount { get; internal set; }

    /// <summary>
    /// Gets the configured interval.
    /// </summary>
    public TimeExtent Interval { get; internal set; }

    /// <summary>
    /// Gets the amount of time that actually elapsed between
    /// the previous and the current timer event.
    /// </summary>
    public TimeExtent IntervalElapsed { get; internal set; }

    /// <summary>
    /// Gets the average interval that actually elapsed of
    /// the last few events.
    /// </summary>
    public TimeExtent IntervalAverage { get; internal set; }

    /// <summary>
    /// The average times per second that the event is firing.
    /// </summary>
    public double Frequency { get; internal set; }

    /// <summary>
    /// Gets the standard deviation from the configured
    /// <see cref="Interval"/> of the last few events.
    /// </summary>
    public TimeExtent IntervalJitter { get; internal set; }

    /// <summary>
    /// Gets the amount of time that has elapsed since the instance
    /// of the <see cref="PrecisionThreadBase"/> was started.
    /// </summary>
    public TimeExtent NaturalElapsed { get; internal set; }

    /// <summary>
    /// Gets the amount of time that has elapsed, computed by adding up 
    /// all the discrete intervals of each individual timer event.
    /// </summary>
    public TimeExtent DiscreteElapsed { get; internal set; }

    /// <summary>
    /// Signals the caller of this event that the worker loop should stop executing cycles.
    /// Setting its value to true will shutdown the loop and no more cycles will be executed.
    /// </summary>
    public bool IsStopRequested { get; set; }

    internal PrecisionCycleEventArgs Clone() => new(
        EventIndex,
        MissedEventCount,
        TotalMissedEventCount,
        Interval,
        IntervalElapsed,
        IntervalAverage,
        Frequency,
        IntervalJitter,
        NaturalElapsed,
        DiscreteElapsed);
}