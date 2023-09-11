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
public sealed class DelayProvider
{
    private static readonly long TightLoopThresholdTicks = Convert.ToInt64(TimeSpan.TicksPerMillisecond * 1.5);

    private readonly TimeSpan RequestedDelay;
    private readonly WinMMTimerCallback TimerCallback;
    private readonly long StartTimestamp;

    private uint UserConext;
    private volatile uint TimerId;
    private object? EventSignal;
    private CancellationToken TimerCancellationToken = CancellationToken.None;

    /// <summary>
    /// Creates a new instance of the <see cref="DelayProvider"/> class.
    /// </summary>
    /// <param name="startTimestamp">A required reference to a starting <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="delay">The requested delay expressed as a <see cref="TimeSpan"/></param>
    private DelayProvider(long startTimestamp, TimeSpan delay)
    {
        StartTimestamp = startTimestamp;
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
        using var mre = new ManualResetEventSlim(false);
        var p = new DelayProvider(startTimestamp, delay)
        {
            EventSignal = mre
        };

        p.BeginTimerWaitLoop(ct);
        p.WaitForTimerSignalDone(ct);
    }

    /// <summary>
    /// Blocks execution of the current thread until the specified
    /// time delay has elapsed.
    /// </summary>
    /// <param name="delay">The requested amount of time to block.</param>
    /// <param name="ct">The optional cancellation token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task DelayAsync(TimeSpan delay, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var p = new DelayProvider(startTimestamp, delay);

        // Create a TCS referencing the delay provider so that
        // it does not go out of scope while the task is running.
        var tcs = new TaskCompletionSource(p, TaskCreationOptions.PreferFairness);

        // Set the event signal to be the TCS
        p.EventSignal = tcs;

        p.BeginTimerWaitLoop(ct);
        return tcs.Task;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginTimerWaitLoop(CancellationToken ct)
    {
        TimerCancellationToken = ct;
        HandleTimerCallback(TimerId, default, ref UserConext, default, default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalTimerDone()
    {
        if (EventSignal is ManualResetEventSlim timerDoneEvent)
            timerDoneEvent.Set();
        else if (EventSignal is TaskCompletionSource taskCompletionSource)
            taskCompletionSource.SetResult();
        else
            throw new InvalidOperationException($"Unsupported {nameof(EventSignal)}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WaitForTimerSignalDone(CancellationToken ct)
    {
        if (EventSignal is not ManualResetEventSlim timerDoneEvent)
            throw new InvalidOperationException($"'{nameof(EventSignal)}' has to be of type {nameof(ManualResetEventSlim)} in order to wait for it synchronously.");

        while (!timerDoneEvent.Wait(1, CancellationToken.None))
        {
            if (ct.IsCancellationRequested)
                break;
        }
    }

    private void HandleTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2)
    {
        // Check if the time has elpased already or a cancellation was issued.
        if (Stopwatch.GetElapsedTime(StartTimestamp).Ticks >= RequestedDelay.Ticks ||
            TimerCancellationToken.IsCancellationRequested)
        {
            SignalTimerDone();
            return;
        }

        // Tight loop for sub-millisecond delay
        // TODO: Provide an option to disable the tight loop and reduce CPU usage
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
}

