using Unosquare.Hpet.Infrastructure;

namespace Unosquare.Hpet.Playground;

internal class Program
{
    private const DelayPrecision Precision = DelayPrecision.Maximum;
    private const double IntervalMillis = 10;

    private static readonly TimeSpan Interval = TimeSpan.FromTicks(Convert.ToInt64(IntervalMillis * TimeSpan.TicksPerMillisecond));

    static async Task Main(string[] args)
    {
        var scheduler = CreatePrecisionTask();
        scheduler.Start();
        Console.ReadKey(true);
        scheduler.Dispose();
        await scheduler.WaitForExitAsync();
        Console.WriteLine("Sample finished.");
    }

    private static IPrecisionLoop CreatePrecisionThread() =>
        new PrecisionThread(Print,
        Interval,
        Precision);

    private static IPrecisionLoop CreatePrecisionTask() =>
        new PrecisionTask(async (e, ct) =>
        {
            if (!ct.IsCancellationRequested)
                await Task.Delay(0).ConfigureAwait(false);

            Print(e);

            if (e.NaturalElapsed.TotalSeconds >= 5)
                e.IsStopRequested = true;
        },
        Interval,
        Precision);

    private static IPrecisionLoop CreatePrecisionTimer()
    {
        var timer = new PrecisionTimer(Interval, Precision);
        timer.Ticked += (s, e) => Print(e);
        return timer;
    }

    private static void Print(PrecisionCycleEventArgs e)
    {
        Console.CursorLeft = 0;
        Console.CursorTop = 0;

        Console.WriteLine($"""
                Period:       {e.Interval.TotalMilliseconds,16:N4} ms.
                Number:       {e.EventIndex,16}
                Elapsed:      {e.IntervalElapsed.TotalMilliseconds,16:N4} ms.
                Average:      {e.IntervalAverage.TotalMilliseconds,16:N4} ms.
                Jitter:       {e.IntervalJitter.TotalMilliseconds,16:N4} ms.
                Missed :      {e.MissedEventCount,16} cycles
                Sum Missed:   {e.TotalMissedEventCount,16} cycles
                Discrete:     {e.DiscreteElapsed.TotalMilliseconds,16:N4} ms.
                Natural:      {e.NaturalElapsed.TotalMilliseconds,16:N4} ms.
                """);
    }
}