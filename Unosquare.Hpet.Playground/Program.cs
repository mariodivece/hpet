using System.Diagnostics;

namespace Unosquare.Hpet.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        RunPrecisionThreadSample(10);
        //RunAsyncDelaySample();
        
    }

    private static void RunPrecisionThreadSample(double intervalMilliseconds)
    {
        var interval = CreateIntervalMillis(intervalMilliseconds);
        var precisionThread = new PrecisionThread((e) =>
        {
            PrintEventToConcolse(e);
            Console.WriteLine("Ticked!");
            //Thread.Sleep(50);
            //DelayProvider.Delay(TimeSpan.FromTicks(Convert.ToInt64(7 * TimeSpan.TicksPerMillisecond)), DelayPrecision.Maximum);
        },
        interval,
        DelayPrecision.Maximum);

        precisionThread.Start();
        Console.ReadKey(true);
        precisionThread.Dispose();
        Console.WriteLine("Disposed!");
    }

    private static void RunAsyncDelaySample()
    {
        var cts = new CancellationTokenSource();
        _ = RunAsyncDelaySampleTask(cts.Token);
        Console.ReadKey();
        cts.Cancel();
    }

    private static async Task RunAsyncDelaySampleTask(CancellationToken ct)
    {
        var interval = CreateIntervalMillis(100);
        long tickNumber = 0;
        var sw = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            sw.Restart();
            tickNumber++;
            await DelayProvider.DelayAsync(interval, DelayPrecision.Maximum, ct).ConfigureAwait(false);
            Console.WriteLine($"Ticked {tickNumber,10} SW: {sw.Elapsed.TotalMilliseconds,16:N4}");
        }
        Console.WriteLine("Finished!");
    }

    private static void PrintEventToConcolse(PrecisionCycleEventArgs e)
    {
        Console.CursorLeft = 0;
        Console.CursorTop = 0;

        Console.WriteLine($"""
                Period:       {e.Interval.TotalMilliseconds,16:N4} ms.
                Number:       {e.EventIndex,16}
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