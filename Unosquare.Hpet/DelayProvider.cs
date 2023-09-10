using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

internal sealed class DelayProvider
{
    private static readonly long StopwatchTicksPerMillisecond = Convert.ToInt64(Stopwatch.Frequency / 1000d);

    private readonly TimeSpan Delay;
    private readonly WinMMTimerCallback TimerCallback;
    private readonly long StartTimestamp;

    private long IsWaiting;
    private uint UserConext;
    private volatile uint TimerId;

    public DelayProvider(TimeSpan delay)
    {
        StartTimestamp = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref IsWaiting);
        Delay = delay;
        TimerCallback = new(HandleTimerCallback);
    }

    public TimeSpan Wait(CancellationToken ct = default)
    {
        HandleTimerCallback(TimerId, default, ref UserConext, default, default);
        WaitForTimerSignalDone(ct);
        return Stopwatch.GetElapsedTime(StartTimestamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalTimerDone()
    {
        Interlocked.Decrement(ref IsWaiting);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WaitForTimerSignalDone(CancellationToken ct)
    {
        SpinWait mre = default;
        while (Interlocked.Read(ref IsWaiting) > 0 && !ct.IsCancellationRequested)
            mre.SpinOnce();
    }

    private void HandleTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2)
    {
        // Check if the time was elapsed or cancellation was requested
        if (Stopwatch.GetElapsedTime(StartTimestamp) >= Delay)
        {
            SignalTimerDone();
            return;
        }

        // Tight loop for sub-millisecond delay
        if (Delay.Ticks - Stopwatch.GetElapsedTime(StartTimestamp).Ticks <= StopwatchTicksPerMillisecond)
        {
            var spinner = default(SpinWait);

            while (Stopwatch.GetElapsedTime(StartTimestamp).Ticks < Delay.Ticks)
            {
                if (!spinner.NextSpinWillYield)
                    spinner.SpinOnce();
            }

            SignalTimerDone();
            return;
        }

        // Queue the handler to be run again at maximum precision.
        TimerId = NativeMethods.TimeSetEvent(
            Constants.OneMillisecond,
            Constants.MaximumPossiblePrecision,
            TimerCallback,
            ref UserConext,
            Constants.EventTypeSingle);

        // Handle exceptions first unblocking waiting thread.
        if (TimerId <= 0)
        {
            SignalTimerDone();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create multimedia timer reference.");
        }
    }
}

