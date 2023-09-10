using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

public sealed class DelayProvider : IDisposable
{
    private static readonly long StopwatchTicksPerMillisecond = Convert.ToInt64(Stopwatch.Frequency / 1000d);

    private readonly TimeSpan RequestedDelay;
    private readonly TimeSpan TimerThresholdDelay;
    private readonly WinMMTimerCallback TimerCallback;
    private readonly long StartTimestamp;
    private readonly ManualResetEventSlim TimerDoneEvent;

    private uint UserConext;
    private volatile uint TimerId;
    private long IsDisposed;

    private DelayProvider(long startTimestamp, TimeSpan delay)
    {
        StartTimestamp = startTimestamp;
        TimerDoneEvent = new(false);
        RequestedDelay = delay;
        TimerThresholdDelay = TimeSpan.FromTicks(RequestedDelay.Ticks - StopwatchTicksPerMillisecond);
        TimerCallback = new(HandleTimerCallback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delay(TimeSpan delay, CancellationToken ct)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        using var p = new DelayProvider(startTimestamp, delay);
        p.Wait(ct);
    }

    private void Wait(CancellationToken ct = default)
    {
        HandleTimerCallback(TimerId, default, ref UserConext, default, default);
        WaitForTimerSignalDone(ct);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalTimerDone()
    {
        TimerDoneEvent.Set();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WaitForTimerSignalDone(CancellationToken ct)
    {
        while (!TimerDoneEvent.Wait(1, CancellationToken.None))
        {
            if (ct.IsCancellationRequested)
                break;
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

    private void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref IsDisposed) > 1)
            return;

        TimerDoneEvent.Set();

        if (alsoManaged)
        {
            TimerDoneEvent.Dispose();
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }
}

