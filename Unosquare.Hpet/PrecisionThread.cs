using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Hpet;

/// <summary>
/// Represents a mostly drop-in replacement for a background <see cref="Thread"/>
/// which executes cycles on monotonic, high precision intervals.
/// </summary>
public class PrecisionThread : IDisposable
{
    private long IsDisposed;
    private readonly Thread WorkerThread;
    private readonly Action<PrecisionTickEventArgs> UserCycleAction;
    private readonly CancellationTokenSource TokenSource = new();

    public PrecisionThread(Action<PrecisionTickEventArgs> cycleAction, TimeSpan interval)
    {
        Interval = interval;
        UserCycleAction = cycleAction;
        WorkerThread = new(WorkerThreadLoop)
        {
            IsBackground = true
        };
    }

    public void Start() => WorkerThread.Start();

    public string? Name
    {
        get => WorkerThread.Name;
        set => WorkerThread.Name = value;
    }

    private void WorkerThreadLoop()
    {
        var eventDurations = new Queue<long>(128);

        var eventState = new PrecisionTickEventArgs();
        var nextInterval = Interval;
        var naturalStartTimestamp = default(long);
        var discreteElapsed = TimeSpan.Zero;
        var tickStartTimestamp = default(long);
        var intervalElapsed = TimeSpan.Zero;
        var driftOffsetTicks = default(long);

        eventState.TickNumber = 1;
        eventState.Interval = nextInterval;

        while (Interlocked.Read(ref IsDisposed) <= 0)
        {
            UserCycleAction?.Invoke(eventState.Clone());

            if (GetElapsedTime(tickStartTimestamp).Ticks < nextInterval.Ticks)
                DelayHelper.Delay(TimeSpan.FromTicks(nextInterval.Ticks - GetElapsedTime(tickStartTimestamp).Ticks), TokenSource.Token);

            nextInterval = TimeSpan.FromTicks(Interval.Ticks - (tickStartTimestamp != default
                ? (GetElapsedTime(tickStartTimestamp).Ticks - nextInterval.Ticks)
                : 0));

            ExchangeTimestamp(ref tickStartTimestamp, out intervalElapsed);

            // push the new state for the next cycle.
            eventState.Interval = intervalElapsed;
            eventState.DiscreteElapsed = TimeSpan.FromTicks(eventState.DiscreteElapsed.Ticks + intervalElapsed.Ticks);
            if (eventState.TickNumber <= 1)
            {
                naturalStartTimestamp = tickStartTimestamp;
                eventState.NaturalElapsed = eventState.DiscreteElapsed;
            }
            else
            {
                eventState.NaturalElapsed = GetElapsedTime(naturalStartTimestamp);

                if (eventDurations.Count > 100)
                    _ = eventDurations.Dequeue();

                eventDurations.Enqueue(intervalElapsed.Ticks);
                eventState.IntervalAverage = TimeSpan.FromTicks(Convert.ToInt64(eventDurations.Average()));
                eventState.IntervalJitter = TimeSpan.FromTicks(
                    Convert.ToInt64(Math.Sqrt(eventDurations.Sum(x => Math.Pow(x - Interval.Ticks, 2)) / eventDurations.Count)));
            }

            // compute drifting due to addition of discrete events not matching up.
            driftOffsetTicks = (eventState.NaturalElapsed.Ticks - eventState.DiscreteElapsed.Ticks) % Interval.Ticks;
            nextInterval = TimeSpan.FromTicks(nextInterval.Ticks + driftOffsetTicks);

            // compute missed events.
            if (nextInterval.Ticks <= 0)
            {
                eventState.MissedEventCount = 1 + Convert.ToInt32(-nextInterval.Ticks / Interval.Ticks);
                nextInterval = TimeSpan.FromTicks(-nextInterval.Ticks % Interval.Ticks);
            }
            else
            {
                eventState.MissedEventCount = 0;
            }

            eventState.TickNumber += (1 + eventState.MissedEventCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan GetElapsedTime(long startingTimestamp) => Stopwatch.GetElapsedTime(startingTimestamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExchangeTimestamp(ref long startingTimestamp, out TimeSpan lastElapsed)
    {
        lastElapsed = startingTimestamp == default ? TimeSpan.Zero : GetElapsedTime(startingTimestamp);
        startingTimestamp = Stopwatch.GetTimestamp();
    }

    public TimeSpan Interval { get; }

    /// <summary>
    /// Disposes internal unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="alsoManaged"></param>
    protected virtual void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref IsDisposed) > 1)
            return;

        TokenSource.Cancel();

        if (alsoManaged)
        {
            // TODO: dispose managed state (managed objects)
            TokenSource.Dispose();
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
