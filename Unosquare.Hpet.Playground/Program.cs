using System.Diagnostics;

namespace Unosquare.Hpet.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var interval = TimeSpan.FromMilliseconds(10);
        var totalSkipped = 0;
        var precisionThread = new PrecisionThread((e) =>
        {
            Console.CursorLeft = 0;
            Console.CursorTop = 0;

            totalSkipped += e.MissedEventCount;

            Console.WriteLine($"""
                Period:       {interval.TotalMilliseconds,16:N4} ms.
                Number:       {e.TickNumber,16}
                Ext. Elapsed: {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,16:N4}
                Elapsed:      {e.Interval.TotalMilliseconds,16:N4} ms.
                Average:      {e.IntervalAverage.TotalMilliseconds,16:N4} ms.
                Jitter:       {e.IntervalJitter.TotalMilliseconds,16:N4} ms.
                Skipped:      {e.MissedEventCount,16} cycles
                Total Skip:   {totalSkipped,16} cycles
                Discrete:     {e.DiscreteElapsed.TotalMilliseconds,16:N4} ms.
                Natural:      {e.NaturalElapsed.TotalMilliseconds,16:N4} ms.
                """);

            startTimestamp = Stopwatch.GetTimestamp();

            //Thread.Sleep(1);
        }, interval);

        //precisionTimer.Start();
        precisionThread.Start();
        Console.ReadKey(true);
        precisionThread.Dispose();
        Console.ReadKey(true);
    }

    private static void Timer_Ticked(object? sender, PrecisionTickEventArgs e)
    {
        //throw new NotImplementedException();
    }
}