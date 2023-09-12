using Unosquare.Hpet.Infrastructure;

namespace Unosquare.Hpet;

/// <summary>
/// Represents a basic implementation of a <see cref="PrecisionLoop"/> that
/// spawns a dedicated background <see cref="Thread"/> that schedules
/// a user defined action for every cycle. The use-defined action is guaranteed
/// to be executed by the same background thread.
/// </summary>
public sealed class PrecisionThread : PrecisionThreadBase
{
    private Action<PrecisionCycleEventArgs>? CycleAction;

    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionThread"/> class.
    /// </summary>
    /// <param name="cycleAction">The action to perform in each individual cycle.</param>
    /// <param name="interval">The desired interval.</param>
    /// <param name="precisionOption">The precision strategy.</param>
    public PrecisionThread(Action<PrecisionCycleEventArgs> cycleAction, TimeSpan interval, DelayPrecision precisionOption = DelayPrecision.Default)
        : base(interval, precisionOption)
    {
        CycleAction = cycleAction;
    }

    /// <inheridoc />
    protected override void DoCycleWork(PrecisionCycleEventArgs cycleEvent) => CycleAction?.Invoke(cycleEvent);

    /// <inheridoc />
    protected override void OnWorkerFinished(Exception? exitException)
    {
        CycleAction = null;
        base.OnWorkerFinished(exitException);
    }

}
