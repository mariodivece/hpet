﻿using System.Diagnostics;
using System.Runtime.CompilerServices;

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
        const int IntervalSampleThreshold = 10;

        var eventDurationsCapacity = Convert.ToInt32(Math.Max(IntervalSampleThreshold, 1d / Interval.TotalSeconds));
        var eventDurations = new Queue<long>(eventDurationsCapacity);
        var systemDriftSamples = new Queue<TimeSpan>(eventDurationsCapacity);

        var eventState = new PrecisionTickEventArgs();
        var nextDelay = Interval;
        var naturalStartTimestamp = default(long);
        var systemStartMillisecond = default(long);

        var discreteElapsed = TimeSpan.Zero;
        
        var tickStartTimestamp = default(long);
        var intervalElapsed = TimeSpan.Zero;
        var naturalDriftOffset = TimeSpan.Zero;
        var averageDriftOffset = TimeSpan.Zero;

        eventState.TickNumber = 1;
        eventState.Interval = nextDelay;

        while (Interlocked.Read(ref IsDisposed) <= 0)
        {
            // Invoke the user action with the current state
            UserCycleAction?.Invoke(eventState.Clone());

            // Introduce a delay
            if (GetElapsedTime(tickStartTimestamp).Ticks < nextDelay.Ticks)
                new DelayProvider(TimeSpan.FromTicks(nextDelay.Ticks - GetElapsedTime(tickStartTimestamp).Ticks)).Wait(TokenSource.Token);

            // start measuring the time interval which includes updating the state for the next tick event
            // and computing event statistics for next cycle.
            ExchangeTimestamp(ref tickStartTimestamp, out intervalElapsed);

            // Compute an initial estimated delay for the next cycle
            nextDelay = TimeSpan.FromTicks(Interval.Ticks - (intervalElapsed.Ticks - nextDelay.Ticks));

            // compute actual interval elapsed time taking into account drifting due to addition of
            // discrete events not adding up to the natural time elapsed
            naturalDriftOffset = TimeSpan.FromTicks((eventState.NaturalElapsed.Ticks - eventState.DiscreteElapsed.Ticks) % Interval.Ticks);
            eventState.Interval = TimeSpan.FromTicks(intervalElapsed.Ticks + naturalDriftOffset.Ticks);

            // Add the interval elapsed to the discrete elapsed
            eventState.DiscreteElapsed = TimeSpan.FromTicks(eventState.DiscreteElapsed.Ticks + eventState.Interval.Ticks);

            if (eventState.TickNumber <= 1)
            {
                // on the first tick, start counting the natural time elapsed
                naturalStartTimestamp = tickStartTimestamp;
                systemStartMillisecond = Environment.TickCount64;
                eventState.NaturalElapsed = eventState.DiscreteElapsed;
            }
            else
            {
                // Update the natural elapsed time
                eventState.NaturalElapsed = GetElapsedTime(naturalStartTimestamp);

                var systemElapsed = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * (Environment.TickCount64 - systemStartMillisecond));
                var systemDriftSample = TimeSpan.FromTicks(eventState.NaturalElapsed.Ticks - systemElapsed.Ticks);
                systemDriftSamples.Enqueue(systemDriftSample);

                if (systemDriftSamples.Count > eventDurationsCapacity)
                    _ = systemDriftSamples.Dequeue();

                Console.CursorTop = 16;
                Console.WriteLine($"{systemDriftSamples.Average(c => c.TotalMilliseconds),16:N4} ms");

                if (eventDurations.Count >= eventDurationsCapacity)
                    _ = eventDurations.Dequeue();

                // Push a sample tot he analysis set.
                eventDurations.Enqueue(eventState.Interval.Ticks);

                // Compute the average.
                eventState.IntervalAverage = TimeSpan.FromTicks(
                    Convert.ToInt64(eventDurations.Average()));

                // Jitter is the standard deviation.
                eventState.IntervalJitter = TimeSpan.FromTicks(
                    Convert.ToInt64(Math.Sqrt(eventDurations.Sum(x => Math.Pow(x - Interval.Ticks, 2)) / eventDurations.Count)));
            }

            // compute drifting to account for average event duration
            if (eventDurations.Count >= IntervalSampleThreshold)
            {
                averageDriftOffset = TimeSpan.FromTicks(eventState.IntervalAverage.Ticks - Interval.Ticks);
                nextDelay = TimeSpan.FromTicks(nextDelay.Ticks - averageDriftOffset.Ticks);
            }
            
            // compute missed events.
            if (nextDelay.Ticks <= 0)
            {
                eventState.MissedEventCount = 1 + Convert.ToInt32(-nextDelay.Ticks / Interval.Ticks);
                nextDelay = TimeSpan.FromTicks(-nextDelay.Ticks % Interval.Ticks);
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
