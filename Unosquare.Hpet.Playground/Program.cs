using System.Diagnostics;

namespace Unosquare.Hpet.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var interval = TimeSpan.FromMilliseconds(10);
        var totalSkipped = 0;
        var timer = new PrecisionThread((s) =>
        {
            Console.CursorLeft = 0;
            Console.CursorTop = 0;

            totalSkipped += s.MissedEventCount;

            Console.WriteLine($"""
                Period:       {interval.TotalMilliseconds,16:N4} ms.
                Number:       {s.TickNumber,16}
                Ext. Elapsed: {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,16:N4}
                Elapsed:      {s.Interval.TotalMilliseconds,16:N4} ms.
                Average:      {s.IntervalAverage.TotalMilliseconds,16:N4} ms.
                Jitter:       {s.IntervalJitter.TotalMilliseconds,16:N4} ms.
                Skipped:      {s.MissedEventCount,16} cycles
                Total Skip:   {totalSkipped,16} cycles
                Discrete:     {s.DiscreteElapsed.TotalMilliseconds,16:N4} ms.
                Natural:      {s.NaturalElapsed.TotalMilliseconds,16:N4} ms.
                """);

            startTimestamp = Stopwatch.GetTimestamp();

            //Thread.Sleep(1);
        }, interval);

        timer.Start();
        Console.ReadKey(true);
    }

    private static void Timer_Ticked(object? sender, PrecisionTickEventArgs e)
    {
        //throw new NotImplementedException();
    }
}