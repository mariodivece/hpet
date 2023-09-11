using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unosquare.Hpet;

public abstract class PrecisionLoopBase : IDisposable
{
    private readonly object SyncLock = new();
    private long m_IsDisposed;
    private WeakReference<CancellationTokenSource>? TokenSourceReference;

    protected PrecisionLoopBase(TimeSpan interval, DelayPrecision precisionOption)
    {
        Interval = interval.Ticks <= 0 ? TimeSpan.FromMilliseconds(1) : interval;
        PrecisionOption = precisionOption;
    }

    /// <summary>
    /// Gets the requested interval at which cycles are to be monotonically executed.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Gets the delay precision strategy to employ.
    /// </summary>
    protected DelayPrecision PrecisionOption { get; }

    /// <summary>
    /// Gets a value indicating whether the <see cref="Dispose()"/> method has been called
    /// and the running worker loop is about to exit or already has.
    /// </summary>
    protected bool IsDisposeRequested => Interlocked.Read(ref m_IsDisposed) > 0;

    /// <summary>
    /// Starts the worker thread loop and begins executing cycles.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when <see cref="Dispose()"/> has been called before.</exception>
    public abstract void Start();

    /// <summary>
    /// Initializes a <see cref="CancellationTokenSource"/> and saves a reference to it
    /// so that a call to <see cref="Dispose()"/> signals the cancellation of such token.
    /// </summary>
    /// <returns>The created and saved <see cref="CancellationTokenSource"/></returns>
    protected CancellationTokenSource CaptureTokenSource()
    {
        var tokenSource = new CancellationTokenSource();
        TokenSourceReference = new(tokenSource);
        return tokenSource;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalCancellation()
    {
        lock (SyncLock)
        {
            if (TokenSourceReference is null || !TokenSourceReference.TryGetTarget(out var tokenSource))
                return;

            TokenSourceReference = null;

            try
            {
                tokenSource.Cancel();
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Disposes internal unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="alsoManaged"></param>
    protected virtual void Dispose(bool alsoManaged)
    {
        if (Interlocked.Increment(ref m_IsDisposed) > 1)
            return;

        SignalCancellation();

        if (alsoManaged)
        {
            // TODO: dispose managed state (managed objects)
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }
}
