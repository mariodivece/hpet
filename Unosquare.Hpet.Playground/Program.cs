using Unosquare.Hpet.Infrastructure;

namespace Unosquare.Hpet.Playground;

internal class Program
{
    // Sample configuration
    private const DelayPrecision Precision = DelayPrecision.Maximum;
    private const double CyclesPerSecond = 75d; // FPS
    private const double RuntimeSeconds = -1; // Set to -1 for no limit

    // Initialization of variables
    private static readonly TimeExtent Runtime = RuntimeSeconds;
    private static readonly TimeExtent Interval = TimeExtent.FromHertz(CyclesPerSecond);

    static async Task Main(string[] args)
    {
        var scheduler = CreatePrecisionTimer();
        scheduler.Start();
        Console.ReadKey(true);
        scheduler.Dispose();
        await scheduler.WaitForExitAsync();
        Console.WriteLine("Sample finished.");
    }

    private static IPrecisionLoop CreatePrecisionThread() =>
        new PrecisionThread((e) =>
        {
            Print(e);

            if (Runtime > TimeExtent.Zero && e.NaturalElapsed >= Runtime)
                e.IsStopRequested = true;
        },
        Interval,
        Precision);

    private static IPrecisionLoop CreatePrecisionTask() =>
        new PrecisionTask(async (e, ct) =>
        {
            if (!ct.IsCancellationRequested)
                await Task.Delay(0, CancellationToken.None).ConfigureAwait(false);

            Print(e);

            if (Runtime > TimeExtent.Zero && e.NaturalElapsed >= Runtime)
                e.IsStopRequested = true;
        },
        Interval,
        Precision);

    private static IPrecisionLoop CreatePrecisionTimer()
    {
        var timer = new PrecisionTimer(Interval, Precision);
        timer.Ticked += (s, e) =>
        {
            Print(e);

            if (Runtime > TimeExtent.Zero && e.NaturalElapsed >= Runtime)
                e.IsStopRequested = true;
        };

        return timer;
    }

    private static void Print(PrecisionCycleEventArgs e)
    {
        Console.CursorLeft = 0;
        Console.CursorTop = 0;

        Console.WriteLine($"""
                Period:       {e.Interval.Milliseconds,16:N4} ms.
                Number:       {e.EventIndex,16}
                Elapsed:      {e.IntervalElapsed.Milliseconds,16:N4} ms.
                Average:      {e.IntervalAverage.Milliseconds,16:N4} ms.
                Frequency:    {e.Frequency,16:N4} Hz.
                Jitter:       {e.IntervalJitter.Milliseconds,16:N4} ms.
                Missed :      {e.MissedEventCount,16} cycles
                Sum Missed:   {e.TotalMissedEventCount,16} cycles
                Discrete:     {e.DiscreteElapsed.Milliseconds,16:N4} ms.
                Natural:      {e.NaturalElapsed.Milliseconds,16:N4} ms.
                """);
    }
}