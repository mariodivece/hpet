namespace Unosquare.Hpet;

/// <summary>
/// Emulates a monotonic, High Precision Event Timer (HPET) while attempting
/// to avoid CPU busy waits as much as possible and accounting for
/// time drifts between <see cref="Ticked"/> events.
/// </summary>
public partial class PrecisionTimer : PrecisionThreadBase
{
    // TODO: https://stackoverflow.com/questions/1416139/how-to-get-timestamp-of-tick-precision-in-net-c
    // TODO: Add a way to get or set TimeSlice timeBeginPeriod and timeEndPeriod and output a disposable
    // TODO: Encapsulate GetTimestamp with calibrated precise as FileTime wwith stopwatch
    // TODO: Stopwatch will drift about 5 ms per hour.

    /// <summary>
    /// Subscribe to this event to execute code when the <see cref="PrecisionTimer"/> ticks.
    /// </summary>
    public event EventHandler<PrecisionTickEventArgs>? Ticked;
    
    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionTimer"/> and starts ticking inmmediately.
    /// </summary>
    /// <param name="interval">The ticking interval expressed as a <see cref="TimeSpan"/>. The minimum interval is 1 millisecond.</param>
    public PrecisionTimer(TimeSpan interval)
        : base(interval)
    {
        // placeholder
    }

    protected override void ExecuteCycle(PrecisionTickEventArgs tickEvent)
    {
        Ticked?.Invoke(this, tickEvent);
    }

}