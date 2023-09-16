#pragma warning disable CA1810 // Initialize reference type static fields inline

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

/// <summary>
/// Provides CPU balanced and precise (sub-millisecond) mechanisms to
/// block and wait for a given time interval before having the current thread proceed.
/// This class is not intended to be inherited or instantiated. Use the static methods
/// available instead.
/// </summary>
public sealed class DelayProvider
{
    private static readonly uint MinimumSystemPeriodMillis;

    /// <summary>
    /// Initializes static fields for the <see cref="DelayProvider"/> utility class.
    /// </summary>
    static DelayProvider()
    {
        var timerCaps = default(TimeCaps);
        NativeMethods.TimeGetDevCaps(ref timerCaps, Constants.SizeOfTimeCaps);
        MinimumSystemPeriodMillis = Math.Max(1, timerCaps.ResolutionMinPeriod);
    }

    /// <summary>
    /// Blocks execution of the current thread until the specified
    /// time delay has elapsed.
    /// </summary>
    /// <param name="delay">The requested amount of time to block.</param>
    /// <param name="precision">The delay precision option.</param>
    /// <param name="ct">The optional cancellation token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delay(TimeSpan delay, DelayPrecision precision = DelayPrecision.Default, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        if (delay.Ticks <= 0)
            return;

        var tightLoopThreshold = ComputeTightLoopThreshold(precision);

        try
        {
            _ = NativeMethods.TimeBeginPeriod(MinimumSystemPeriodMillis);
            while (!SleepOne(startTimestamp, delay, tightLoopThreshold, ct))
            {
                // keep sleeping
            }
        }
        finally
        {
            _ = NativeMethods.TimeEndPeriod(MinimumSystemPeriodMillis);
        }
    }

    /// <summary>
    /// Blocks execution of the current thread until the specified
    /// time delay has elapsed.
    /// </summary>
    /// <param name="delay">The requested amount of time to block.</param>
    /// /// <param name="precision">The delay precision option.</param>
    /// <param name="ct">The optional cancellation token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask DelayAsync(TimeSpan delay, DelayPrecision precision = DelayPrecision.Default, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        if (delay.Ticks <= 0)
            return;

        var tightLoopThreshold = ComputeTightLoopThreshold(precision);

        try
        {
            _ = NativeMethods.TimeBeginPeriod(MinimumSystemPeriodMillis);
            while (await SleepOneAsync(startTimestamp, delay, tightLoopThreshold, ct).ConfigureAwait(false) == false)
            {
                // keep sleeping
            }
        }
        finally
        {
            _ = NativeMethods.TimeEndPeriod(MinimumSystemPeriodMillis);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SleepOne(long startTimestamp, TimeSpan requestedDelay, TimeSpan tightLoopThreshold, CancellationToken ct)
    {
        // Check if the time has elpased already or a cancellation was issued.
        if (HasElapsed(startTimestamp, requestedDelay, ct))
            return true;

        // Tight loop for sub-millisecond delay precision.
        if (NeedsTightLoopWait(startTimestamp, requestedDelay, tightLoopThreshold))
        {
            var spinner = default(SpinWait);

            while (!HasElapsed(startTimestamp, requestedDelay, ct))
            {
                if (!spinner.NextSpinWillYield)
                    spinner.SpinOnce();
            }

            return true;
        }

        Thread.Sleep(1);

        return HasElapsed(startTimestamp, requestedDelay, ct);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<bool> SleepOneAsync(long startTimestamp, TimeSpan requestedDelay, TimeSpan tightLoopThreshold, CancellationToken ct)
    {
        // Check if the time has elpased already or a cancellation was issued.
        if (HasElapsed(startTimestamp, requestedDelay, ct))
            return true;

        // Tight loop for sub-millisecond delay precision.
        if (NeedsTightLoopWait(startTimestamp, requestedDelay, tightLoopThreshold))
        {
            var spinner = default(SpinWait);

            while (!HasElapsed(startTimestamp, requestedDelay, ct))
            {
                if (!spinner.NextSpinWillYield)
                    spinner.SpinOnce();
            }

            return true;
        }

        await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);

        return HasElapsed(startTimestamp, requestedDelay, ct);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan ComputeTightLoopThreshold(DelayPrecision precision)
    {
        var tightLoopFactor = precision switch
        {
            DelayPrecision.Default => 0d,
            DelayPrecision.Medium => 2d / 3d,
            DelayPrecision.High => 4d / 3d,
            DelayPrecision.Maximum => 6d / 3d,
            _ => 0d
        };

        return TimeSpan.FromTicks(
            Convert.ToInt64(TimeSpan.TicksPerMillisecond * MinimumSystemPeriodMillis * tightLoopFactor));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasElapsed(long startTimestamp, TimeSpan requestedDelay, CancellationToken ct) =>
        ct.IsCancellationRequested || Stopwatch.GetElapsedTime(startTimestamp).Ticks >= requestedDelay.Ticks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NeedsTightLoopWait(long startTimestamp, TimeSpan requestedDelay, TimeSpan tightLoopThreshold) =>
        tightLoopThreshold.Ticks > 0 &&
        requestedDelay.Ticks - Stopwatch.GetElapsedTime(startTimestamp).Ticks <= tightLoopThreshold.Ticks;
}
#pragma warning restore CA1810 // Initialize reference type static fields inline