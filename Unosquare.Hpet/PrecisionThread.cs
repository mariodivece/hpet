namespace Unosquare.Hpet;

public class PrecisionThread : PrecisionThreadBase
{
    private readonly Action<PrecisionTickEventArgs> CycleAction;
    private readonly TaskCompletionSource WorkerExitTaskSource;

    public PrecisionThread(Action<PrecisionTickEventArgs> cycleAction, TimeSpan interval, DelayPrecision precisionOption = DelayPrecision.Maximum)
        : base(interval, precisionOption)
    {
        CycleAction = cycleAction;
        WorkerExitTaskSource = new(this);
    }

    /// <inheridoc />
    protected override void RunWorkerCycle(PrecisionTickEventArgs tickEvent)
    {
        CycleAction.Invoke(tickEvent);
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

    public Task WaitForFinishedAsync() => WorkerExitTaskSource.Task;
}
