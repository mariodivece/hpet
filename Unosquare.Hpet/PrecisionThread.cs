namespace Unosquare.Hpet;

public class PrecisionThread : PrecisionThreadBase
{
    private readonly Action<PrecisionTickEventArgs> UserCallback;

    public PrecisionThread(Action<PrecisionTickEventArgs> userCallback, TimeSpan interval) : base(interval)
    {
        UserCallback = userCallback;
    }

    protected override void ExecuteCycle(PrecisionTickEventArgs tickEvent)
    {
        UserCallback.Invoke(tickEvent);
    }
}
