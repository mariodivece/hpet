using System.Diagnostics;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

public partial class PrecisionTimer
{
    private static readonly long StopwatchTicksPerMillisecond = Convert.ToInt64(Stopwatch.Frequency / 1000d);

    /// <summary>
    /// Introduces a synchronous time delay,
    /// and using a CPU busy wait as a last resort.
    /// </summary>
    /// <param name="delay">The time delay to introduce. Must be positive.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>The actual delay that was introduced.</returns>
    public static TimeSpan Delay(TimeSpan delay, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        if (delay.Ticks <= 0)
            return Stopwatch.GetElapsedTime(startTimestamp);

        using var mre = new ManualResetEventSlim(false);
        uint userContext = default;
        WinMMTimerCallback? handler = null;

        handler = new WinMMTimerCallback((uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2) =>
        {
            if (Stopwatch.GetElapsedTime(startTimestamp) >= delay || ct.IsCancellationRequested)
            {
                mre.Set();
                return;
            }

            // Tight loop for sub-millisecond delay
            if (delay.Ticks - Stopwatch.GetElapsedTime(startTimestamp).Ticks <= StopwatchTicksPerMillisecond)
            {
                var spinner = default(SpinWait);

                while (Stopwatch.GetElapsedTime(startTimestamp).Ticks < delay.Ticks)
                {
                    if (!spinner.NextSpinWillYield)
                        spinner.SpinOnce();
                }

                mre.Set();
                return;
            }

            // Queue the handler to be run again
            var timerId = NativeMethods.TimeSetEvent(
                Constants.OneMillisecond,
                Constants.MaximumPossiblePrecision,
                handler!,
                ref userContext,
                Constants.EventTypeSingle);
        });

        handler.Invoke(default, default, ref userContext, default, default);
        mre.Wait(delay, ct);
        return Stopwatch.GetElapsedTime(startTimestamp);
    }

    /// <summary>
    /// Introduces an asynchronous time delay,
    /// and using a CPU busy wait as a last resort.
    /// </summary>
    /// <param name="delay">The time delay to introduce. Must be positive.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>The actual delay that was introduced.</returns>
    public static Task<TimeSpan> DelayAsync(TimeSpan delay, CancellationToken ct = default) =>
        Task.Run(() => Delay(delay, ct));
}
