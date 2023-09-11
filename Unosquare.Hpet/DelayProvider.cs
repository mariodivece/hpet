using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet;

/// <summary>
/// Provides CPU balanced and precise (sub-millisecond) mechanisms to
/// block and wait for a given time interval before having the current thread proceed.
/// This class is not intended to be inherited or instantiated. Use the static methods
/// available instead.
/// </summary>
public sealed class DelayProvider : IDisposable
{
    private static readonly long TightLoopThresholdTicks = Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1.5);

    private readonly TimeSpan RequestedDelay;
    private readonly WinMMTimerCallback TimerCallback;
    private readonly long StartTimestamp;
    private readonly ManualResetEventSlim TimerDoneEvent;

    private uint UserConext;
    private volatile uint TimerId;
    private long IsDisposed;

    /// <summary>
    /// Creates a new instance of the <see cref="DelayProvider"/> class.
    /// </summary>
    /// <param name="startTimestamp">A required reference to a starting <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="delay">The requested delay expressed as a <see cref="TimeSpan"/></param>
    private DelayProvider(long startTimestamp, TimeSpan delay)
    {
        StartTimestamp = startTimestamp;
        TimerDoneEvent = new(false);
        RequestedDelay = delay;
        TimerCallback = new(HandleTimerCallback);
    }

    /// <summary>
    /// Blocks execution of the current thread until the specified
    /// time delay has elapsed.
    /// </summary>
    /// <param name="delay">The requested amount of time to block.</param>
    /// <param name="ct">The optional cancellation token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delay(TimeSpan delay, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        using var p = new DelayProvider(startTimestamp, delay);
        p.Wait(ct);
    }

    /// <summary>
    /// Blocks execution of the current thread until the specified
    /// time delay has elapsed.
    /// </summary>
    /// <param name="milliseconds">The requested number of milliseconds to block.</param>
    /// <param name="ct">The optional cancellation token.</param>
    public static void Delay(int milliseconds, CancellationToken ct = default) =>
        Delay(TimeSpan.FromTicks(Convert.ToInt64(milliseconds * TimeSpan.TicksPerMillisecond)), ct);

    /// <summary>
    /// Returns a task that completes after the specified delay.
    /// </summary>
    /// <param name="delay">The requested amount of time delay befor the task completes.</param>
    /// <param name="ct">The optional cancellation token.</param>
    public static Task DelayAsync(TimeSpan delay, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        return Task.Run(() =>
        {
            using var p = new DelayProvider(startTimestamp, delay);
            p.Wait(ct);
        }, CancellationToken.None);
    }

    /// <summary>
    /// Returns a task that completes after the specified delay.
    /// </summary>
    /// <param name="milliseconds">The requested number of milliseconds befor the task completes.</param>
    /// <param name="ct">The optional cancellation token.</param>
    public static Task DelayAsync(int milliseconds, CancellationToken ct = default) =>
        DelayAsync(TimeSpan.FromTicks(Convert.ToInt64(milliseconds * TimeSpan.TicksPerMillisecond)), ct);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        // Check if the time has elpased already.
        if (Stopwatch.GetElapsedTime(StartTimestamp).Ticks >= RequestedDelay.Ticks)
        {
            SignalTimerDone();
            return;
        }

        // Tight loop for sub-millisecond delay
        if (RequestedDelay.Ticks - Stopwatch.GetElapsedTime(StartTimestamp).Ticks <= TightLoopThresholdTicks)
        {
            var spinner = default(SpinWait);

            while (Stopwatch.GetElapsedTime(StartTimestamp).Ticks < RequestedDelay.Ticks)
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

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }
}

