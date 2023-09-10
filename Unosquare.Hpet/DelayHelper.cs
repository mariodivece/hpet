﻿using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

internal static class DelayHelper
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

        var mre = new ManualResetEvent(false);
        uint userContext = default;

        // setup a callback for the timer
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

            if (timerId <= 0)
            {
                mre.Set();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

        });


        handler.Invoke(default, default, ref userContext, default, default);
        mre.WaitOne(); // delay, false);

        GC.KeepAlive(mre);
        GC.KeepAlive(handler);
        mre.Dispose();


        return Stopwatch.GetElapsedTime(startTimestamp);
    }

}
