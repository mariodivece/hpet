using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Unosquare.Hpet;

/// <summary>
/// Represents as <see cref="TimeSpan"/> backed type that can
/// have an undeterminate value <see cref="IsNaN"/> and that can perform
/// comparison and arithmetic operations with <see cref="double"/> and
/// <see cref="TimeSpan"/>. Please note that <see cref="double"/> arithmetic
/// is always represented in seconds. This class provides a much more
/// streamlined and precise way of dealing with timing operations.
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public readonly struct TimeExtent :
    INumber<TimeExtent>,
    IMinMaxValue<TimeExtent>,

    IEquatable<TimeSpan>,
    IEquatable<double>,

    IComparable<TimeSpan>,
    IComparable<double>,

    IEqualityOperators<TimeExtent, TimeSpan, bool>,
    IEqualityOperators<TimeExtent, double, bool>,

    IAdditionOperators<TimeExtent, TimeSpan, TimeExtent>,
    IAdditionOperators<TimeExtent, double, TimeExtent>,

    ISubtractionOperators<TimeExtent, TimeSpan, TimeExtent>,
    ISubtractionOperators<TimeExtent, double, TimeExtent>,

    IMultiplyOperators<TimeExtent, TimeSpan, TimeExtent>,
    IMultiplyOperators<TimeExtent, double, TimeExtent>,

    IDivisionOperators<TimeExtent, TimeSpan, TimeExtent>,
    IDivisionOperators<TimeExtent, double, TimeExtent>,

    IModulusOperators<TimeExtent, TimeSpan, TimeExtent>,
    IModulusOperators<TimeExtent, double, TimeExtent>
{
    private static readonly TimeExtent ConstantZero = new();
    private static readonly TimeExtent ConstantOne = new(TimeSpan.FromSeconds(1));
    private static readonly TimeExtent ConstantNaN = new(TimeSpan.MinValue);
    private static readonly TimeExtent ConstantMin = new(TimeSpan.FromTicks(long.MinValue + 1));
    private static readonly TimeExtent ConstantMax = new(TimeSpan.MaxValue);

    private readonly bool m_IsNan = false;
    private readonly TimeSpan m_Value = TimeSpan.Zero;

    #region Constructors

    /// <summary>
    /// Creates a new instance of the <see cref="TimeExtent"/> struct.
    /// </summary>
    public TimeExtent()
    {
        // placeholder
    }

    private TimeExtent(bool isNaN, TimeSpan value)
    {
        m_IsNan = isNaN;
        m_Value = value;
    }

    private TimeExtent(TimeSpan value)
        : this(isNaN: CheckNaN(value), value: value)
    {
        // placeholder
    }

    private TimeExtent(double seconds)
        : this(MakeTimeSpan(seconds))
    {
        // placeholder
    }

    #endregion

    #region Constants

    /// <inheritdoc />
    public static TimeExtent One => ConstantOne;

    /// <inheritdoc />
    public static TimeExtent Zero => ConstantZero;

    /// <summary>
    /// Returns an instance of <see cref="TimeExtent"/> representing NaN (unspecified).
    /// </summary>
    public static TimeExtent NaN => ConstantNaN;

    /// <inheritdoc />
    public static TimeExtent MaxValue => ConstantMax;

    /// <inheritdoc />
    public static TimeExtent MinValue => ConstantMin;


    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether this instance contains an
    /// undefined value. If set to true, any arithmetic operations
    /// will result in undefined instances of this struct.
    /// </summary>
    public readonly bool IsNaN => m_IsNan;

    /// <summary>
    /// Gets the backing <see cref="TimeSpan"/> value. If <see cref="IsNaN"/>
    /// is set to true, then this value will return <see cref="TimeSpan.MinValue"/>.
    /// </summary>
    public readonly TimeSpan Value => m_Value;

    /// <summary>
    /// Gets the time value expressed in seconds. If <see cref="IsNaN"/>
    /// is set to true, then this value will return <see cref="double.NaN"/>.
    /// </summary>
    public readonly double Seconds => IsNaN ? double.NaN : Value.TotalSeconds;

    /// <summary>
    /// Gets the time value expressed in milliseconds. If <see cref="IsNaN"/>
    /// is set to true, then this value will return <see cref="double.NaN"/>.
    /// </summary>
    public readonly double Milliseconds => IsNaN ? double.NaN : Value.TotalMilliseconds;

    /// <inheritdoc />
    static int INumberBase<TimeExtent>.Radix => 2;

    /// <inheritdoc />
    public static TimeExtent AdditiveIdentity => ConstantZero;

    /// <inheritdoc />
    public static TimeExtent MultiplicativeIdentity => ConstantOne;

    #endregion

    #region CompareTo

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj is null
        ? 1
        : obj is TimeExtent te
        ? CompareTo(te)
        : obj is double d
        ? CompareTo(d)
        : obj is TimeSpan ts
        ? CompareTo(ts)
        : throw new ArgumentException("Argument has incompatible comparison type.", nameof(obj));

    /// <inheritdoc />
    public readonly int CompareTo(TimeExtent other) => !IsNaN
        ? Value.CompareTo(other.Value)
        : IsNaN.CompareTo(other.IsNaN);

    /// <inheritdoc />
    public readonly int CompareTo(TimeSpan other) => IsNaN && CheckNaN(other)
        ? 0
        : !IsNaN && CheckNaN(other)
        ? 1
        : IsNaN && !CheckNaN(other)
        ? -1
        : Value.CompareTo(other);

    /// <inheritdoc />
    public readonly int CompareTo(double other) => IsNaN && CheckNaN(other)
        ? 0
        : !IsNaN && CheckNaN(other)
        ? 1
        : IsNaN && !CheckNaN(other)
        ? -1
        : Value.CompareTo(other);

    #endregion

    #region Equals

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TimeExtent other && Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(TimeExtent other) => CompareTo(other) == 0;

    /// <inheritdoc />
    public readonly bool Equals(TimeSpan other) => CompareTo(other) == 0;

    /// <inheritdoc />
    public readonly bool Equals(double other) => CompareTo(other) == 0;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    #endregion

    #region Equality Operators

    /// <inheritdoc />
    public static bool operator ==(TimeExtent left, TimeExtent right) => !left.IsNaN && !right.IsNaN && left.Equals(right);

    /// <inheritdoc />
    public static bool operator ==(TimeExtent left, TimeSpan right) => left == (TimeExtent)right;

    /// <inheritdoc />
    public static bool operator ==(TimeExtent left, double right) => left == (TimeExtent)right;

    /// <inheritdoc />
    public static bool operator !=(TimeExtent left, TimeExtent right) => !(left == right);

    /// <inheritdoc />
    public static bool operator !=(TimeExtent left, TimeSpan right) => left != (TimeExtent)right;

    /// <inheritdoc />
    public static bool operator !=(TimeExtent left, double right) => left != (TimeExtent)right;

    #endregion

    #region Comparison Operators

    /// <inheritdoc />
    public static bool operator <(TimeExtent left, TimeExtent right) => !left.IsNaN && !right.IsNaN && left.CompareTo(right) < 0;

    /// <inheritdoc />
    public static bool operator <=(TimeExtent left, TimeExtent right) => !left.IsNaN && !right.IsNaN && left.CompareTo(right) <= 0;

    /// <inheritdoc />
    public static bool operator >(TimeExtent left, TimeExtent right) => !left.IsNaN && !right.IsNaN && left.CompareTo(right) > 0;

    /// <inheritdoc />
    public static bool operator >=(TimeExtent left, TimeExtent right) => !left.IsNaN && !right.IsNaN && left.CompareTo(right) >= 0;

    #endregion

    #region Implicit Casting

    /// <summary>
    /// Converts from <see cref="TimeSpan"/> to <see cref="TimeExtent"/>.
    /// </summary>
    /// <param name="other">The value to convert to <see cref="TimeExtent"/></param>
    public static implicit operator TimeExtent(TimeSpan other) => CheckNaN(other) ? ConstantNaN : new(other);

    /// <summary>
    /// Converts from <see cref="TimeExtent"/> to <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="other">The value to convert to <see cref="TimeSpan"/></param>
    public static implicit operator TimeSpan(TimeExtent other) => other.IsNaN ? TimeSpan.MinValue : other.Value;

    /// <summary>
    /// Converts the current instance of the <see cref="TimeExtent"/> to a <see cref="TimeSpan"/>.
    /// If <see cref="IsNaN"/> is set, returns <see cref="TimeSpan.MinValue"/>.
    /// </summary>
    /// <returns>The <see cref="TimeSpan"/> value.</returns>
    public TimeSpan ToTimeSpan() => Value;

    /// <summary>
    /// Converts from <see cref="double"/> (expressed in seconds) to <see cref="TimeExtent"/>.
    /// </summary>
    /// <param name="other">The value to convert to <see cref="TimeExtent"/></param>
    public static implicit operator TimeExtent(double other) => CheckNaN(other) ? ConstantNaN : new(other);

    /// <summary>
    /// Creates a <see cref="TimeExtent"/> instance from a <see cref="double"/> expressed in seconds.
    /// This method has the exact same behavior as the <see cref="FromSeconds(double)"/> method.
    /// </summary>
    /// <param name="value">The value to convert expressed in seconds.</param>
    /// <returns>The <see cref="TimeExtent"/> value.</returns>
    public static TimeExtent FromDouble(double value) => FromSeconds(value);

    /// <summary>
    /// Converts from <see cref="TimeExtent"/> to <see cref="double"/> expressed in seconds.
    /// </summary>
    /// <param name="other">The value to convert to <see cref="double"/></param>
    public static implicit operator double(TimeExtent other) => other.IsNaN ? double.NaN : other.Seconds;

    /// <summary>
    /// Converts the current instance of the <see cref="TimeExtent"/> to a <see cref="double"/>
    /// expressed in seconds. If <see cref="IsNaN"/> is set, returns <see cref="double.NaN"/>.
    /// </summary>
    /// <returns>The <see cref="double"/> value.</returns>
    public double ToDouble() => Seconds;

    #endregion

    #region Unary Operators

    /// <summary>
    /// Returns the value of the <see cref="TimeExtent"/>.
    /// </summary>
    /// <param name="left">The <see cref="TimeExtent"/>.</param>
    /// <returns>The value of the <see cref="TimeExtent"/>.</returns>
    public static TimeExtent operator +(TimeExtent left) => left.IsNaN ? ConstantNaN : left.Value;

    /// <summary>
    /// Returns the negated value of the <see cref="TimeExtent"/>.
    /// </summary>
    /// <param name="left">The <see cref="TimeExtent"/>.</param>
    /// <returns>The negated value of the <see cref="TimeExtent"/>.</returns>
    public static TimeExtent operator -(TimeExtent left) => left.IsNaN ? ConstantNaN : TimeSpan.FromTicks(-left.Value.Ticks);

    #endregion

    #region Increment and Decrement

    /// <summary>
    /// Increments the value by 1 second.
    /// </summary>
    /// <param name="left">The argument.</param>
    /// <returns>The incremented value.</returns>
    public static TimeExtent operator ++(TimeExtent left) => left.IsNaN ? ConstantNaN : left + 1d;

    /// <summary>
    /// Decrements the value by 1 second.
    /// </summary>
    /// <param name="left">The argument.</param>
    /// <returns>The decremented value.</returns>
    public static TimeExtent operator --(TimeExtent left) => left.IsNaN ? ConstantNaN : left - 1d;

    #endregion

    #region Arithmetic Basis

    /// <summary>
    /// Computes the addition of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator +(TimeExtent left, TimeExtent right) => left.IsNaN || right.IsNaN
        ? ConstantNaN : TimeSpan.FromTicks(left.Value.Ticks + right.Value.Ticks);

    /// <summary>
    /// Computes the subtraction of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator -(TimeExtent left, TimeExtent right) => left.IsNaN || right.IsNaN
        ? ConstantNaN : TimeSpan.FromTicks(left.Value.Ticks - right.Value.Ticks);

    /// <summary>
    /// Computes the multiplication of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator *(TimeExtent left, TimeExtent right) => left.IsNaN || right.IsNaN
        ? ConstantNaN : MakeTimeSpan(left.Value.TotalSeconds * right.Value.TotalSeconds);

    /// <summary>
    /// Computes the division of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator /(TimeExtent left, TimeExtent right) => left.IsNaN || right.IsNaN
        ? ConstantNaN : MakeTimeSpan(left.Value.TotalSeconds / right.Value.TotalSeconds);

    /// <summary>
    /// Computes the modulus of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator %(TimeExtent left, TimeExtent right) => left.IsNaN || right.IsNaN
        ? ConstantNaN : MakeTimeSpan(left.Value.TotalSeconds % right.Value.TotalSeconds);

    #endregion

    #region Arithmetic overloads

    /// <summary>
    /// Computes the addition of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator +(TimeExtent left, TimeSpan right) => left + (TimeExtent)right;

    /// <summary>
    /// Computes the addition of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator +(TimeSpan left, TimeExtent right) => (TimeExtent)left + right;

    /// <summary>
    /// Computes the addition of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator +(TimeExtent left, double right) => left + (TimeExtent)right;

    /// <summary>
    /// Computes the addition of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator +(double left, TimeExtent right) => (TimeExtent)left + right;

    /// <summary>
    /// Computes the subtraction of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator -(TimeExtent left, TimeSpan right) => left - (TimeExtent)right;

    /// <summary>
    /// Computes the subtraction of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator -(TimeSpan left, TimeExtent right) => (TimeExtent)left - right;

    /// <summary>
    /// Computes the subtraction of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator -(TimeExtent left, double right) => left - (TimeExtent)right;

    /// <summary>
    /// Computes the subtraction of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator -(double left, TimeExtent right) => (TimeExtent)left - right;

    /// <summary>
    /// Computes the multiplication of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator *(TimeExtent left, TimeSpan right) => left * (TimeExtent)right;

    /// <summary>
    /// Computes the multiplication of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator *(TimeSpan left, TimeExtent right) => (TimeExtent)left * right;

    /// <summary>
    /// Computes the multiplication of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator *(TimeExtent left, double right) => left * (TimeExtent)right;

    /// <summary>
    /// Computes the multiplication of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator *(double left, TimeExtent right) => (TimeExtent)left * right;

    /// <summary>
    /// Computes the division of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator /(TimeExtent left, TimeSpan right) => left / (TimeExtent)right;

    /// <summary>
    /// Computes the division of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator /(TimeSpan left, TimeExtent right) => (TimeExtent)left / right;

    /// <summary>
    /// Computes the division of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator /(TimeExtent left, double right) => left / (TimeExtent)right;

    /// <summary>
    /// Computes the division of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator /(double left, TimeExtent right) => (TimeExtent)left / right;

    /// <summary>
    /// Computes the modulus of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator %(TimeExtent left, TimeSpan right) => left % (TimeExtent)right;

    /// <summary>
    /// Computes the modulus of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator %(TimeSpan left, TimeExtent right) => (TimeExtent)left % right;

    /// <summary>
    /// Computes the modulus of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator %(TimeExtent left, double right) => left % (TimeExtent)right;

    /// <summary>
    /// Computes the modulus of 2 values.
    /// </summary>
    /// <param name="left">the left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The resulting value.</returns>
    public static TimeExtent operator %(double left, TimeExtent right) => (TimeExtent)left % right;

    #endregion

    #region Instance Methods (Inmutable)

    /// <summary>
    /// Provides a new <see cref="TimeExtent"/> with an absolute value.
    /// If <see cref="IsNaN"/> is true, then the newly produced value will also contain a set <see cref="IsNaN"/>.
    /// </summary>
    /// <returns></returns>
    public readonly TimeExtent Abs() => IsNaN
        ? ConstantNaN
        : Value >= TimeSpan.Zero
        ? Value
        : TimeSpan.FromTicks(-Value.Ticks);

    /// <summary>
    /// Returns the string representation of the <see cref="Seconds"/> value
    /// with 4 fixed decimal places and in <see cref="CultureInfo.InvariantCulture"/>.
    /// If <see cref="IsNaN"/> then it returns the NaN string.
    /// </summary>
    /// <returns>The string representation of this structure.</returns>
    public override string ToString() => ToString("n4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the string representation of the <see cref="Seconds"/> value
    /// If <see cref="IsNaN"/> then it returns the NaN string.
    /// </summary>
    /// <returns>The string representation of this structure.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => IsNaN
        ? double.NaN.ToString(format, formatProvider)
        : Seconds.ToString(format, formatProvider);

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates an instance of <see cref="TimeExtent"/> struct based on a <see cref="TimeSpan"/>.
    /// Note that if the <see cref="TimeSpan"/> is set to <see cref="TimeSpan.MinValue"/> then <see cref="IsNaN"/>
    /// will be set to true.
    /// </summary>
    /// <param name="other">The time represented in a <see cref="TimeSpan"/>.</param>
    /// <returns>The new instance.</returns>
    public static TimeExtent FromTimeSpan(TimeSpan other) => CheckNaN(other) ? ConstantNaN : new(other);

    /// <summary>
    /// Creates an instance of <see cref="TimeExtent"/> struct based on number of seconds.
    /// Note that if the <see cref="double"/> is set to <see cref="double.NaN"/> then <see cref="IsNaN"/>
    /// will be set to true.
    /// </summary>
    /// <param name="seconds">The time represented in seconds.</param>
    /// <returns>The new instance.</returns>
    public static TimeExtent FromSeconds(double seconds) => CheckNaN(seconds) ? ConstantNaN : new(seconds);

    /// <summary>
    /// Creates an instance of <see cref="TimeExtent"/> struct based on number of milliseconds.
    /// Note that if the <see cref="double"/> is set to <see cref="double.NaN"/> then <see cref="IsNaN"/>
    /// will be set to true.
    /// </summary>
    /// <param name="milliseconds">The time represented in milliseconds.</param>
    /// <returns>The new instance.</returns>
    public static TimeExtent FromMilliseconds(double milliseconds) => CheckNaN(milliseconds) ? ConstantNaN : new(milliseconds / 1000d);

    /// <summary>
    /// Creates an instance of <see cref="TimeExtent"/> struct.
    /// </summary>
    /// <param name="sw">The <see cref="Stopwatch"/>.</param>
    /// <returns>The new instance.</returns>
    /// <exception cref="ArgumentNullException">The stopwatch instance must not be null.</exception>
    public static TimeExtent FromStopwatch(Stopwatch sw) => sw is null
        ? throw new ArgumentNullException(nameof(sw))
        : new(sw.Elapsed);

    /// <summary>
    /// Retrieves a <see cref="TimeExtent"/> instance based on relative system time.
    /// This is an easy way t get a system timestamp expressed in <see cref="TimeSpan"/>.
    /// </summary>
    /// <returns>The new instance.</returns>
    public static TimeExtent FromSystem() => TimeSpan.FromTicks(Convert.ToInt64(
        TimeSpan.TicksPerSecond * (Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency)));

    /// <summary>
    /// Retrieves a <see cref="TimeExtent"/> instance based on a timestamp previously
    /// obtained from <see cref="Stopwatch.GetTimestamp"/> and up to the current system
    /// timestamp.
    /// </summary>
    /// <param name="startStopwatchTimestamp"></param>
    /// <returns>The new instance.</returns>
    public static TimeExtent FromElapsed(long startStopwatchTimestamp) =>
        Stopwatch.GetElapsedTime(startStopwatchTimestamp);

    /// <summary>
    /// Retrieves a <see cref="TimeExtent"/> instance based on cycles per second.
    /// </summary>
    /// <param name="cyclesPerSecond">The number of cycles per second.</param>
    /// <returns>The new instance.</returns>
    public static TimeExtent FromHertz(double cyclesPerSecond) =>
        CheckNaN(cyclesPerSecond) ? ConstantNaN : new(1d / cyclesPerSecond);

    #endregion

    #region Rest of INumber implementation

    /// <inheritdoc />
    public static TimeExtent Abs(TimeExtent value) => double.Abs(value.Seconds);

    /// <inheritdoc />
    public static bool IsInteger(TimeExtent value) => double.IsInteger(value.Seconds);

    /// <inheritdoc />
    public static bool IsOddInteger(TimeExtent value) => double.IsOddInteger(value.Seconds);

    /// <inheritdoc />
    public static bool IsEvenInteger(TimeExtent value) => double.IsEvenInteger(value.Seconds);

    /// <inheritdoc />
    public static bool IsFinite(TimeExtent value) => double.IsFinite(value.Seconds);

    /// <inheritdoc />
    public static bool IsImaginaryNumber(TimeExtent value) => false;

    /// <inheritdoc />
    public static bool IsInfinity(TimeExtent value) => double.IsInfinity(value.Seconds);

    /// <inheritdoc />
    public static bool IsNegativeInfinity(TimeExtent value) => double.IsNegativeInfinity(value.Seconds);

    /// <inheritdoc />
    public static bool IsPositiveInfinity(TimeExtent value) => double.IsPositiveInfinity(value.Seconds);

    /// <inheritdoc />
    public static bool IsNegative(TimeExtent value) => double.IsNegative(value.Seconds);

    /// <inheritdoc />
    public static bool IsPositive(TimeExtent value) => double.IsPositive(value.Seconds);

    /// <inheritdoc />
    public static bool IsRealNumber(TimeExtent value) => double.IsRealNumber(value.Seconds);

    /// <inheritdoc />
    public static bool IsNormal(TimeExtent value) => double.IsNormal(value.Seconds);

    /// <inheritdoc />
    public static bool IsSubnormal(TimeExtent value) => double.IsSubnormal(value.Seconds);

    /// <inheritdoc />
    public static bool IsZero(TimeExtent value) => value == ConstantZero;

    /// <inheritdoc />
    public static TimeExtent MaxMagnitude(TimeExtent x, TimeExtent y) => Math.MaxMagnitude(x.Seconds, y.Seconds);

    /// <inheritdoc />
    public static TimeExtent MinMagnitude(TimeExtent x, TimeExtent y) => Math.MinMagnitude(x.Seconds, y.Seconds);

    /// <inheritdoc />
    public static TimeExtent MaxMagnitudeNumber(TimeExtent x, TimeExtent y) => double.MaxMagnitudeNumber(x.Seconds, y.Seconds);

    /// <inheritdoc />
    public static TimeExtent MinMagnitudeNumber(TimeExtent x, TimeExtent y) => double.MinMagnitudeNumber(x.Seconds, y.Seconds);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.IsNaN(TimeExtent value) => double.IsNaN(value.Seconds);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.IsCanonical(TimeExtent value) => true;

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.IsComplexNumber(TimeExtent value) => false;

    #endregion

    #region Parsing and Formatting

    /// <inheritdoc />
    public static TimeExtent Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => IsTimeSpanString(s)
        ? TimeSpan.Parse(s, provider)
        : double.Parse(s, style, provider);

    /// <inheritdoc />
    public static TimeExtent Parse(string s, NumberStyles style, IFormatProvider? provider) =>
        Parse(s.AsSpan(), style, provider);

    /// <inheritdoc />
    public static TimeExtent Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, NumberStyles.Any, provider);

    /// <inheritdoc />
    public static TimeExtent Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), NumberStyles.Any, provider);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out TimeExtent result)
    {
        result = ConstantNaN;

        if (double.TryParse(s, provider, out var resultDouble))
        {
            result = resultDouble;
            return true;
        }

        if (TimeSpan.TryParse(s, provider, out var resultTimeSpan))
        {
            result = resultTimeSpan;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out TimeExtent result) =>
        TryParse(s.AsSpan(), style, provider, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out TimeExtent result) =>
        TryParse(s, NumberStyles.Any, provider, out result);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TimeExtent result) =>
        TryParse(s.AsSpan(), NumberStyles.Any, provider, out result);

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Seconds.TryFormat(destination, out charsWritten, format, provider) ||
        Value.TryFormat(destination, out charsWritten, format, provider);

    #endregion

    #region Conversion

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.TryConvertFromChecked<TOther>(TOther value, out TimeExtent result) =>
        TryConvertToTimeExtent(value, out result);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.TryConvertFromSaturating<TOther>(TOther value, out TimeExtent result) =>
        TryConvertToTimeExtent(value, out result);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.TryConvertFromTruncating<TOther>(TOther value, out TimeExtent result) =>
        TryConvertToTimeExtent(value, out result);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.TryConvertToChecked<TOther>(TimeExtent value, out TOther result) =>
        TryConvertFromTimeExtent(value, out result!);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.TryConvertToSaturating<TOther>(TimeExtent value, out TOther result) =>
        TryConvertFromTimeExtent(value, out result!);

    /// <inheritdoc />
    static bool INumberBase<TimeExtent>.TryConvertToTruncating<TOther>(TimeExtent value, out TOther result) =>
        TryConvertFromTimeExtent(value, out result!);

    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan MakeTimeSpan(double seconds) => CheckNaN(seconds)
        ? TimeSpan.MinValue
        : TimeSpan.FromTicks(Convert.ToInt64(TimeSpan.TicksPerSecond * seconds));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckNaN(double other) => !double.IsFinite(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckNaN(TimeSpan other) => other == TimeSpan.MinValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTimeSpanString(ReadOnlySpan<char> s) => s.Contains(":", StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertToTimeExtent<TOther>(TOther value, out TimeExtent result)
    {
        result = ConstantNaN;

        // Fast fail
        if (value is null)
            return false;

        // Fast conversions
        if (value is double d)
        {
            result = FromDouble(d);
            return true;
        }

        if (value is TimeSpan t)
        {
            result = FromTimeSpan(t);
            return true;
        }

        if (value is string str)
            return TryParse(str.AsSpan(), CultureInfo.CurrentCulture, out result);

        if (value is IConvertible conv)
        {
            try
            {
                result = FromDouble(conv.ToDouble(CultureInfo.CurrentCulture));
                return true;
            }
            catch
            {
                // ignore
            }

            try
            {
                str = conv.ToString(null);
                return TryParse(str.AsSpan(), CultureInfo.CurrentCulture, out result);
            }
            catch
            {
                // ignore
            }

        }

        // Using Parsing as a last resort.
        if (double.TryParse((value.ToString() ?? string.Empty).AsSpan(), CultureInfo.CurrentCulture, out var resultString))
        {
            result = resultString;
            return true;
        }

        // everything else failed.
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFromTimeExtent<TOther>(TimeExtent value, out TOther? result)
    {
        result = default;

        // Fast conversions
        if (typeof(TOther) == typeof(double))
        {
            result = (TOther)(object)value.Seconds;
            return true;
        }

        if (typeof(TOther) == typeof(TimeSpan))
        {
            result = (TOther)(object)value.Value;
            return true;
        }

        return false;
    }

    #endregion
}
