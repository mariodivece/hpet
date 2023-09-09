namespace Unosquare.Hpet;

/// <summary>
/// Represents data associated with the event arguments of a <see cref="PrecisionTimer.Ticked"/> event.
/// </summary>
public class PrecisionTickEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionTickEventArgs"/>
    /// </summary>
    /// <param name="tickNumber"></param>
    /// <param name="missedEventCount"></param>
    /// <param name="interval"></param>
    /// <param name="intervalAverage"></param>
    /// <param name="intevalJitter"></param>
    /// <param name="naturalElapsed"></param>
    /// <param name="discreteElapsed"></param>
    internal PrecisionTickEventArgs(
        long tickNumber = default,
        int missedEventCount = default,
        TimeSpan interval = default,
        TimeSpan intervalAverage = default,
        TimeSpan intevalJitter = default,
        TimeSpan naturalElapsed = default,
        TimeSpan discreteElapsed = default)
        : base()
    {
        TickNumber = tickNumber;
        MissedEventCount = missedEventCount;
        Interval = interval;
        IntervalAverage = intervalAverage;
        IntervalJitter = intevalJitter;
        NaturalElapsed = naturalElapsed;
        DiscreteElapsed = discreteElapsed;
    }

    /// <summary>
    /// The consecutive tick event number starting from 1.
    /// </summary>
    public virtual long TickNumber { get; internal set; }

    /// <summary>
    /// The number of timer events that were not fired due to excessive time
    /// taken to fire the event internally or synchronously handle the event.
    /// </summary>
    public virtual int MissedEventCount { get; internal set; }

    /// <summary>
    /// The amount of time elapsed between the previous and
    /// the current timer event.
    /// </summary>
    public virtual TimeSpan Interval { get; internal set; }

    /// <summary>
    /// The average of the time elapsed between each of the last
    /// 100 timer events.
    /// </summary>
    public virtual TimeSpan IntervalAverage { get; internal set; }

    /// <summary>
    /// The standard deviation of the time elapsed between each of the last
    /// 100 timer events.
    /// </summary>
    public virtual TimeSpan IntervalJitter { get; internal set; }

    /// <summary>
    /// The amount of time that has elapsed since the instance
    /// of the <see cref="PrecisionTimer"/> was created.
    /// </summary>
    public virtual TimeSpan NaturalElapsed { get; internal set; }

    /// <summary>
    /// The amount of time that has elapsed, computed by adding up 
    /// all the discrete intervals of each individual fired timer event.
    /// </summary>
    public virtual TimeSpan DiscreteElapsed { get; internal set; }


    internal PrecisionTickEventArgs Clone() =>
        new(TickNumber, MissedEventCount, Interval, IntervalAverage, IntervalJitter, NaturalElapsed, DiscreteElapsed);
}