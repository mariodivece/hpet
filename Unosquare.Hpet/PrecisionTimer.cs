namespace Unosquare.Hpet;

/// <summary>
/// Emulates a monotonic, High Precision Event Timer (HPET) while attempting
/// to avoid CPU busy waits as much as possible and accounting for
/// adjustments time drifts between <see cref="Ticked"/> events.
/// </summary>
public partial class PrecisionTimer : PrecisionThreadBase
{
    /// <summary>
    /// Subscribe to this event to execute code when the <see cref="PrecisionTimer"/> ticks.
    /// </summary>
    public event EventHandler<PrecisionTickEventArgs>? Ticked;

    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionTimer"/> and starts ticking inmmediately.
    /// </summary>
    /// <param name="interval">The ticking interval expressed as a <see cref="TimeSpan"/>. The minimum interval is 1 millisecond.</param>
    /// <param name="precisionOption">The delay precision option.</param>
    public PrecisionTimer(TimeSpan interval, DelayPrecision precisionOption)
        : base(interval, precisionOption)
    {
        // placeholder
    }

    /// <inheritdoc />
    protected override void RunWorkerCycle(PrecisionTickEventArgs tickEvent)
    {
        Ticked?.Invoke(this, tickEvent);
    }
}