namespace Unosquare.Hpet;

/// <summary>
/// Provides extension methods to introduce precise delays on <see cref="TimeSpan"/> instances.
/// </summary>
public static class DelayExtensions
{
    /// <summary>
    /// Introduces a precise blocking delay.
    /// </summary>
    /// <param name="delay">The desired delay amount.</param>
    /// <param name="precision">The desired precision option.</param>
    /// <param name="ct">The optional <see cref="CancellationToken"/>.</param>
    public static void Delay(this TimeSpan delay, DelayPrecision precision = DelayPrecision.Default, CancellationToken ct = default) =>
        DelayProvider.Delay(delay, precision, ct);

    /// <summary>
    /// Introduces a precise blocking delay.
    /// </summary>
    /// <param name="delay">The desired delay amount.</param>
    /// <param name="precision">The desired precision option.</param>
    /// <param name="ct">The optional <see cref="CancellationToken"/>.</param>
    public static Task DelayAsync(this TimeSpan delay, DelayPrecision precision = DelayPrecision.Default, CancellationToken ct = default) =>
        DelayProvider.DelayAsync(delay, precision, ct);
}
