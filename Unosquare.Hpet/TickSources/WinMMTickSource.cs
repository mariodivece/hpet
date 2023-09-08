using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unosquare.Hpet.WinMM;

namespace Unosquare.Hpet.TickSources;

internal sealed class WinMMTickSource : IDisposable
{
    private long NaturalStartTimestamp;
    private long TickStartTimestamp;
    private long HandleCount;
    private int SkippedHandlerCount;
    private long DiscreteElapsedTicks;
    private readonly TimeCaps TimerCapabilities;
    private uint NullContextPointer;
    private long IsDisposed;
    private readonly WinMMTimerCallback TimerCallback;

    private WinMMTickSource(TimeSpan interval, TimerTickCallback tickHandler)
    {
        var hasReolutionInfo = 0 == NativeMethods.TimeGetDevCaps(
            ref TimerCapabilities, Constants.SizeOfTimeCaps);

        TickHandler = tickHandler;
        Interval = interval;
        NaturalStartTimestamp = GetTimestamp();
        TimerCallback = new(NativeTimerCallback);
        QueueTimerCallback(Interval.TotalMilliseconds, false);
    }

    public TimeSpan Interval { get; }

    private TimerTickCallback TickHandler { get; }

    private TimeSpan TickEventElapsed =>
        GetElapsedTime(Interlocked.Read(ref TickStartTimestamp));

    private TimeSpan NaturalElapsed =>
        GetElapsedTime(Interlocked.Read(ref NaturalStartTimestamp));

    public static WinMMTickSource CreateInstance(TimeSpan interval, TimerTickCallback tickHandler) =>
        new(interval, tickHandler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetTimestamp() =>
        Stopwatch.GetTimestamp();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan GetElapsedTime(long startingTimestamp) =>
        Stopwatch.GetElapsedTime(startingTimestamp);

    private void QueueTimerCallback(double delayMs, bool firedEarly)
    {
        var eventElapsed = TimeSpan.Zero;

        if (!firedEarly)
            eventElapsed = UpdateTickEventStartTimestamp();

        NativeQueueTimerEvent(delayMs);

        if (firedEarly || !TryPushTickHandler(eventElapsed, out var skippedHandlers, out var discreteElapsedSum))
            return;

        _ = OffsetTickEventStartTimestamp(discreteElapsedSum);
        var skippedEvents = ComputeSkippedEvents(skippedHandlers, eventElapsed);
        CommitTickHandler(eventElapsed, skippedEvents);
    }

    private void HandleTimerCallback()
    {
        if (Interlocked.Read(ref IsDisposed) > 0)
            return;

        var elapsedMs = TickEventElapsed.TotalMilliseconds;
        var targetDelayMs = Interval.TotalMilliseconds;
        var firedEarly = false;

        if (elapsedMs < Interval.TotalMilliseconds)
        {
            firedEarly = true;
            targetDelayMs = Interval.TotalMilliseconds - elapsedMs;
        }
        else if (elapsedMs > Interval.TotalMilliseconds)
        {
            var offsetDelay = elapsedMs % Interval.TotalMilliseconds;
            targetDelayMs = Interval.TotalMilliseconds - offsetDelay;
        }

        if (targetDelayMs <= Math.Max(1, TimerCapabilities.ResolutionMinPeriod))
        {
            SpinWaitUntilIntervalTickElapsed();
            firedEarly = false;
            targetDelayMs = Interval.TotalMilliseconds;
        }

        QueueTimerCallback(targetDelayMs, firedEarly);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ConvertToTimestampOffset(TimeSpan timeSpan) =>
        Convert.ToInt64(timeSpan.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan UpdateTickEventStartTimestamp()
    {
        var previousStartTimestamp = Interlocked.Exchange(ref TickStartTimestamp, GetTimestamp());
        return previousStartTimestamp <= 0 ? NaturalElapsed : GetElapsedTime(previousStartTimestamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SpinWaitUntilIntervalTickElapsed()
    {
        SpinWait waiter = default;

        while (TickEventElapsed.Ticks < Interval.Ticks)
        {
            // try to spin; otherwise keep running a tight loop.
            if (!waiter.NextSpinWillYield)
                waiter.SpinOnce();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryPushTickHandler(TimeSpan eventElapsed, out int skippedHandlers, out TimeSpan discreteElapsedTotal)
    {
        skippedHandlers = 0;
        discreteElapsedTotal = TimeSpan.Zero;

        if (Interlocked.Read(ref HandleCount) > 0)
        {
            Interlocked.Increment(ref SkippedHandlerCount);
            return false;
        }

        Interlocked.Increment(ref HandleCount);
        skippedHandlers = Interlocked.Exchange(ref SkippedHandlerCount, 0);

        DiscreteElapsedTicks += eventElapsed.Ticks;
        discreteElapsedTotal = TimeSpan.FromTicks(DiscreteElapsedTicks);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ComputeSkippedEvents(int skippedHandlers, TimeSpan eventElapsed)
    {
        if (Interval.Ticks <= 0)
            return default;

        var skippedTimeEvents = eventElapsed.Ticks > Interval.Ticks
            ? Convert.ToInt32(eventElapsed.Ticks / Interval.Ticks) - 1
            : 0;

        return skippedTimeEvents + skippedHandlers;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CommitTickHandler(TimeSpan eventElapsed, int skippedEvents)
    {
        if (Interlocked.Read(ref IsDisposed) > 0)
            return;

        try
        {
            TickHandler.Invoke(eventElapsed, skippedEvents);

            if (Interlocked.Read(ref HandleCount) <= 0)
                throw new InvalidOperationException($"Mismatched '{nameof(TryPushTickHandler)}'");
        }
        catch
        {
            // TODO: Write abstract exception handler
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref HandleCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long OffsetTickEventStartTimestamp(TimeSpan discreteElapsedSum)
    {
        // Compare the discrete elapsed timestamps to natural time elapsed
        // and compute an offset to the start timestamp of the next tick
        // which will yield the actual time (including drift from natural time)
        var timeOffset = TimeSpan.FromTicks(discreteElapsedSum.Ticks - NaturalElapsed.Ticks);
        return Interlocked.Exchange(ref TickStartTimestamp,
            Interlocked.Read(ref TickStartTimestamp) + ConvertToTimestampOffset(timeOffset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint NativeQueueTimerEvent(double delayMs)
    {
        var eventDelay = Math.Max(1, Convert.ToUInt32(Math.Floor(delayMs)));

        var effectiveSplitDelay = eventDelay;
        var splitResolution = Math.Min(5u, eventDelay);
        while (splitResolution >= TimerCapabilities.ResolutionMinPeriod)
        {
            if (effectiveSplitDelay % splitResolution == 0)
            {
                effectiveSplitDelay /= splitResolution;
                break;
            }

            splitResolution--;
        }

        if (effectiveSplitDelay <= 0)
            effectiveSplitDelay = 1;

        var timerEventId = NativeMethods.TimeSetEvent(
            effectiveSplitDelay,
            Constants.MaximumPossiblePrecision,
            TimerCallback,
            ref NullContextPointer,
            Constants.EventTypeSingle);

        return timerEventId < 0
            ? throw new Win32Exception(Marshal.GetLastWin32Error())
            : timerEventId;
    }

    private void NativeTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2) => HandleTimerCallback();

    private void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref IsDisposed) > 1)
            return;

        if (alsoManaged)
        {
            // TODO: dispose managed state (managed objects)
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

