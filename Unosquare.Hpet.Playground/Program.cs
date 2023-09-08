namespace Unosquare.Hpet.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        var timer = new PrecisionTimer(TimeSpan.FromMilliseconds(10));
        timer.Ticked += Timer_Ticked;
        Console.ReadLine();
    }

    private static void Timer_Ticked(object? sender, PrecisionTimerTickedEventArgs e)
    {
        //throw new NotImplementedException();
    }
}