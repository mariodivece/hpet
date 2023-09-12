﻿namespace Unosquare.Hpet;

/// <summary>
/// Represents data associated with timing information of a precision loop cycle.
/// </summary>
public sealed class PrecisionCycleEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionCycleEventArgs"/>
    /// </summary>
    internal PrecisionCycleEventArgs(
        long eventIndex = default,
        int missedEventCount = default,
        TimeSpan interval = default,
        TimeSpan intervalElapsed = default,
        TimeSpan intervalAverage = default,
        TimeSpan intevalJitter = default,
        TimeSpan naturalElapsed = default,
        TimeSpan discreteElapsed = default)
        : base()
    {
        EventIndex = eventIndex;
        MissedEventCount = missedEventCount;
        Interval = interval;
        IntervalElapsed = intervalElapsed;
        IntervalAverage = intervalAverage;
        IntervalJitter = intevalJitter;
        NaturalElapsed = naturalElapsed;
        DiscreteElapsed = discreteElapsed;
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
    /// Gets the configured interval.
    /// </summary>
    public TimeSpan Interval { get; internal set; }

    /// <summary>
    /// Gets the amount of time that actually elapsed between
    /// the previous and the current timer event.
    /// </summary>
    public TimeSpan IntervalElapsed { get; internal set; }

    /// <summary>
    /// Gets the average interval that actually elapsed of
    /// the last few events.
    /// </summary>
    public TimeSpan IntervalAverage { get; internal set; }

    /// <summary>
    /// Gets the standard deviation from the configured
    /// <see cref="Interval"/> of the last few events.
    /// </summary>
    public TimeSpan IntervalJitter { get; internal set; }

    /// <summary>
    /// Gets the amount of time that has elapsed since the instance
    /// of the <see cref="PrecisionThreadBase"/> was started.
    /// </summary>
    public TimeSpan NaturalElapsed { get; internal set; }

    /// <summary>
    /// Gets the amount of time that has elapsed, computed by adding up 
    /// all the discrete intervals of each individual timer event.
    /// </summary>
    public TimeSpan DiscreteElapsed { get; internal set; }


    internal PrecisionCycleEventArgs Clone() =>
        new(EventIndex, MissedEventCount, Interval, IntervalElapsed, IntervalAverage, IntervalJitter, NaturalElapsed, DiscreteElapsed);
}