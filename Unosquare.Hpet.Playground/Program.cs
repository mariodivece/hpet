﻿using System.Diagnostics;
using System.Linq.Expressions;

namespace Unosquare.Hpet.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        //RunPrecisionTimerSample(10);

        var cts = new CancellationTokenSource();
        RunAsyncDelaySample(cts.Token);
        Console.ReadKey();
        cts.Cancel();

        Console.WriteLine("Press any key to continue.");
        Console.ReadKey(true);
    }

    private static void RunPrecisionTimerSample(double intervalMilliseconds)
    {
        var interval = CreateIntervalMillis(intervalMilliseconds);
        var precisionThread = new PrecisionThread((e) =>
        {
            PrintEventToConcolse(e);
            Console.WriteLine("Ticked!");
        }, interval);

        precisionThread.Start();
        Console.ReadKey(true);
        precisionThread.Dispose();
        Console.WriteLine("Disposed!");
    }

    private static async Task RunAsyncDelaySample(CancellationToken ct)
    {
        var interval = CreateIntervalMillis(100);
        long tickNumber = 0;
        Stopwatch sw = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            sw.Restart();
            tickNumber++;
            await DelayProvider.DelayAsync(interval, ct).ConfigureAwait(false);
            Console.WriteLine($"Ticked {tickNumber,10} SW: {sw.ElapsedMilliseconds,16:N4}");
        }

        Console.WriteLine("Finished!");
    }

    private static void PrintEventToConcolse(PrecisionTickEventArgs e)
    {
        Console.CursorLeft = 0;
        Console.CursorTop = 0;

        Console.WriteLine($"""
                Period:       {e.Interval.TotalMilliseconds,16:N4} ms.
                Number:       {e.TickEventNumber,16}
                Elapsed:      {e.IntervalElapsed.TotalMilliseconds,16:N4} ms.
                Average:      {e.IntervalAverage.TotalMilliseconds,16:N4} ms.
                Jitter:       {e.IntervalJitter.TotalMilliseconds,16:N4} ms.
                Skipped:      {e.MissedEventCount,16} cycles
                Discrete:     {e.DiscreteElapsed.TotalMilliseconds,16:N4} ms.
                Natural:      {e.NaturalElapsed.TotalMilliseconds,16:N4} ms.
                """);
    }

    private static TimeSpan CreateIntervalMillis(double milliseconds) =>
        TimeSpan.FromTicks(Convert.ToInt64(milliseconds * TimeSpan.TicksPerMillisecond));
}