using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

internal sealed class DelayProvider
{
    private static readonly long StopwatchTicksPerMillisecond = Convert.ToInt64(Stopwatch.Frequency / 1000d);

    private readonly TimeSpan RequestedDelay;
    private readonly TimeSpan TimerThresholdDelay;
    private readonly WinMMTimerCallback TimerCallback;
    private readonly long StartTimestamp;
    private readonly bool AllowContextSwitching;

    private long IsWaiting;
    private uint UserConext;
    private volatile uint TimerId;

    private DelayProvider(TimeSpan delay, bool allowContextSwitching)
    {
        StartTimestamp = Stopwatch.GetTimestamp();
        AllowContextSwitching = allowContextSwitching;
        Interlocked.Increment(ref IsWaiting);
        RequestedDelay = delay;
        TimerThresholdDelay = TimeSpan.FromTicks(RequestedDelay.Ticks - StopwatchTicksPerMillisecond);
        TimerCallback = new(HandleTimerCallback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delay(TimeSpan delay, bool allowContextSwitching, CancellationToken ct) =>
        new DelayProvider(delay, allowContextSwitching).Wait(ct);

    public void Wait(CancellationToken ct = default)
    {
        HandleTimerCallback(TimerId, default, ref UserConext, default, default);
        WaitForTimerSignalDone(ct);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalTimerDone()
    {
        Interlocked.Decrement(ref IsWaiting);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WaitForTimerSignalDone(CancellationToken ct)
    {
        SpinWait spinner = default;
        while (Interlocked.Read(ref IsWaiting) > 0 || Stopwatch.GetElapsedTime(StartTimestamp).Ticks < RequestedDelay.Ticks)
        {
            if (ct.IsCancellationRequested)
                break;

            if (!spinner.NextSpinWillYield || AllowContextSwitching)
                spinner.SpinOnce();
        }
    }

    private void HandleTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2)
    {
        // Check if the time was elapsed or close (sub-millisecond wait needed) to elapsed
        if (Stopwatch.GetElapsedTime(StartTimestamp).Ticks >= TimerThresholdDelay.Ticks)
        {
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

