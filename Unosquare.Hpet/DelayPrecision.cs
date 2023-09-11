namespace Unosquare.Hpet;

/// <summary>
/// Enumerates the possible options for <see cref="DelayProvider"/>
/// precision options.
/// </summary>
public enum DelayPrecision
{
    /// <summary>
    /// Represents a basic precision with increased 'jittering'
    /// between timing events but avoids any kind of CPU spinning or
    /// busy waits. Such 'jittering' or variation in timing intervals
    /// will be purely dictated by your hardware and OS.
    /// Typically you will want to use this option
    /// when sub-millisecond precision is not critical.
    /// </summary>
    Default,

    /// <summary>
    /// Represents the maximum possible precision that the system can handle
    /// at the expense of slightly increased CPU usage because of spinning and
    /// busy waits that attempt to avoid any kind of context switching for
    /// residual, partial delays. Typically you will want to use this option for
    /// audio or multimedia applications where minimal 'jittering' is needed.
    /// </summary>
    Maximum
}
