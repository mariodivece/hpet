using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unosquare.Hpet;

public partial class DelayProvider
{
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

        using var mre = new ManualResetEventSlim(false);
        var p = new DelayProvider(startTimestamp, delay, precision)
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
    /// /// <param name="precision">The delay precision option.</param>
    /// <param name="ct">The optional cancellation token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task DelayAsync(TimeSpan delay, DelayPrecision precision = DelayPrecision.Default, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        if (delay.Ticks <= 0)
            return Task.CompletedTask;

        var p = new DelayProvider(startTimestamp, delay, precision);

        // Create a TCS referencing the delay provider so that
        // it does not go out of scope while the task is running.
        var tcs = new TaskCompletionSource(p);

        // Set the event signal to be the TCS because we are returning a task.
        p.EventSignal = tcs;

        p.BeginTimerWaitLoop(ct);
        return tcs.Task;
    }

}
