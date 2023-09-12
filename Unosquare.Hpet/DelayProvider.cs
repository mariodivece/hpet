﻿#pragma warning disable CA1810 // Initialize reference type static fields inline

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
public sealed partial class DelayProvider
{
    private static readonly uint MinimumSystemPeriodMillis;

    private readonly TimeSpan TightLoopThreshold;

    private readonly TimeSpan RequestedDelay;
    private readonly WinMMTimerCallback TimerCallback;
    private readonly long StartTimestamp;
    private readonly DelayPrecision PrecisionOption;

    private uint UserConext;
    private volatile uint TimerId;
    private object? EventSignal;
    private CancellationToken TimerCancellationToken = CancellationToken.None;

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
    /// Creates a new instance of the <see cref="DelayProvider"/> class.
    /// </summary>
    /// <param name="startTimestamp">A required reference to a starting <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="delay">The requested delay expressed as a <see cref="TimeSpan"/></param>
    /// <param name="precisionOption">The configured delay precision.</param>
    private DelayProvider(long startTimestamp, TimeSpan delay, DelayPrecision precisionOption)
    {
        StartTimestamp = startTimestamp;
        var tightLoopFactor = precisionOption switch
        {
            DelayPrecision.Default => 0d,
            DelayPrecision.Medium => 2d / 3d,
            DelayPrecision.High => 4d / 3d,
            DelayPrecision.Maximum => 6d / 3d,
            _ => 0d
        };

        TightLoopThreshold = TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerMillisecond * MinimumSystemPeriodMillis * tightLoopFactor));
        RequestedDelay = delay;
        TimerCallback = new(HandleTimerCallback);
        PrecisionOption = precisionOption;
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

        // Tight loop for sub-millisecond delay precision.
        if (TightLoopThreshold.Ticks > 0 &&
            RequestedDelay.Ticks - Stopwatch.GetElapsedTime(StartTimestamp).Ticks <= TightLoopThreshold.Ticks)
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
#pragma warning restore CA1810 // Initialize reference type static fields inline