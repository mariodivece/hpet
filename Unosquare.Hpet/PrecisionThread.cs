namespace Unosquare.Hpet;

public sealed class PrecisionThread : PrecisionThreadBase
{
    private readonly Action<PrecisionCycleEventArgs>? CycleAction;
    private readonly TaskCompletionSource WorkerExitTaskSource;

    public PrecisionThread(Action<PrecisionCycleEventArgs> cycleAction, TimeSpan interval, DelayPrecision precisionOption = DelayPrecision.Default)
        : base(interval, precisionOption)
    {
        CycleAction = cycleAction;
        WorkerExitTaskSource = new(this);
    }

    /// <inheridoc />
    protected override void DoCycleWork(PrecisionCycleEventArgs cycleEvent)
    {
        CycleAction?.Invoke(cycleEvent);
    }

    /// <inheridoc />
    protected override void OnWorkerFinished(Exception? exitException)
    {
        if (exitException is not null)
        {
            WorkerExitTaskSource.TrySetException(exitException);
            return;
        }

        WorkerExitTaskSource.TrySetResult();
    }

    public Task WaitForExitAsync() => WorkerExitTaskSource.Task;
}
