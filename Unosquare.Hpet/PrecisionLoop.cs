﻿using System.Runtime.CompilerServices;

namespace Unosquare.Hpet;

/// <summary>
/// Represents a base class for implementing Precision Timers,
/// Precision Threads or Precision Tasks. This class does not do anything
/// on its own, and therefore it is not recommended that you inherit
/// from it unless a highly customized cycle scheduler implementation is required.
/// </summary>
public abstract class PrecisionLoop : IDisposable
{
    private readonly object SyncLock = new();
    private long m_IsDisposed;
    private long m_StartCallCount;

    private WeakReference<CancellationTokenSource>? TokenSourceReference;

    /// <summary>
    /// Creates a new instance of the <see cref="PrecisionLoop"/> class.
    /// </summary>
    /// <param name="interval">The desired interval. The minimum is 1 <see cref="TimeSpan.Ticks"/>.</param>
    /// <param name="precisionOption">The desired precision strategy.</param>
    protected PrecisionLoop(TimeSpan interval, DelayPrecision precisionOption)
    {
        Interval = interval.Ticks <= 0 ? TimeSpan.FromTicks(1) : interval;
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
    protected bool IsCancellationRequested => Interlocked.Read(ref m_IsDisposed) > 0 ||
        (TokenSourceReference is not null && TokenSourceReference.TryGetTarget(out var tokenSource) && tokenSource.IsCancellationRequested);

    /// <summary>
    /// Starts the worker loop and begins executing cycles.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when <see cref="Dispose()"/> has been called before.</exception>
    public void Start()
    {
        if (IsCancellationRequested)
            throw new ObjectDisposedException(nameof(PrecisionThreadBase));

        if (Interlocked.Increment(ref m_StartCallCount) > 1)
            throw new InvalidOperationException($"The method '{nameof(Start)}' has already been called.");

        StartWorker();
    }

    /// <summary>
    /// Implements the logic to start the worker loop. Must be a non-blocking call.
    /// </summary>
    protected abstract void StartWorker();

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

    /// <summary>
    /// Marks the internal <see cref="CancellationTokenSource"/> as cancelled
    /// and removes it from the internal <see cref="TokenSourceReference"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SignalCancellation()
    {
        lock (SyncLock)
        {
            if (TokenSourceReference is null || !TokenSourceReference.TryGetTarget(out var tokenSource))
                return;

            TokenSourceReference = null;

#pragma warning disable CA1031
            try
            {
                tokenSource.Cancel();
            }
            catch
            {
                // ignore
            }
#pragma warning restore CA1031
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
