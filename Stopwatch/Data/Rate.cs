using Humanizer;

namespace Stopwatch.Data;

/// <summary>
///     Represents a rate of occurrence over a period of time.
/// </summary>
public readonly struct Rate : IComparable<Rate>
{
    private Rate(long count, TimeSpan duration)
    {
        if (count < 0)
        {
            throw new ArgumentException("Count must be greater than or equal to 0.", nameof(count));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentException("Duration must be greater than or equal to 0.", nameof(duration));
        }

        Count = count;
        Duration = duration;
    }

    /// <summary>
    ///     Gets the count.
    /// </summary>
    /// <value>The count.</value>
    public long Count { get; }

    /// <summary>
    ///     Gets the count per second.
    /// </summary>
    /// <value>The count per second.</value>
    public double CountPerSecond => Count / Duration.TotalSeconds;

    /// <summary>
    ///     Gets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; }

    /// <summary>
    ///     Returns a value indicating whether one rate is less than another.
    /// </summary>
    /// <param name="left">The first rate to compare.</param>
    /// <param name="right">The second rate to compare.</param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="left" /> is less than <paramref name="right" />; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public static bool operator <(Rate left, Rate right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    ///     Returns a value indicating whether one rate is greater than another.
    /// </summary>
    /// <param name="left">The first rate to compare.</param>
    /// <param name="right">The second rate to compare.</param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="left" /> is greater than <paramref name="right" />; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public static bool operator >(Rate left, Rate right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    ///     Returns a value indicating whether one rate is less than or equal to another.
    /// </summary>
    /// <param name="left">The first rate to compare.</param>
    /// <param name="right">The second rate to compare.</param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="left" /> is less than or equal to <paramref name="right" />;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    public static bool operator <=(Rate left, Rate right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    ///     Returns a value indicating whether one rate is greater than or equal to another.
    /// </summary>
    /// <param name="left">The first rate to compare.</param>
    /// <param name="right">The second rate to compare.</param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="left" /> is greater than or equal to <paramref name="right" />;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    public static bool operator >=(Rate left, Rate right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    ///     Constructs a rate from the specified count and duration.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <param name="count">The count.</param>
    /// <returns>The constructed rate.</returns>
    public static Rate Per(TimeSpan duration, long count)
    {
        return new Rate(count, duration);
    }

    /// <summary>
    ///     Constructs a rate from the specified count per minute.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>The constructed rate.</returns>
    public static Rate PerMinute(long count)
    {
        return Per(TimeSpan.FromMinutes(1), count);
    }

    /// <summary>
    ///     Compares this instance to a specified rate and returns an indication of their relative values.
    /// </summary>
    /// <param name="other">A rate to compare.</param>
    /// <returns>
    ///     <list type="table">
    ///         <listheader>
    ///             <term>Value</term>
    ///             <description>Meaning</description>
    ///         </listheader>
    ///         <item>
    ///             <term>Less than zero</term>
    ///             <description>This instance is less than <paramref name="other" />.</description>
    ///         </item>
    ///         <item>
    ///             <term>Zero</term>
    ///             <description>This instance is equal to <paramref name="other" />.</description>
    ///         </item>
    ///         <item>
    ///             <term>Greater than zero</term>
    ///             <description>This instance is greater than <paramref name="other" />.</description>
    ///         </item>
    ///     </list>
    /// </returns>
    public int CompareTo(Rate other)
    {
        return CountPerSecond.CompareTo(other.CountPerSecond);
    }

    /// <summary>
    ///     Returns a value indicating whether this rate exceeds the specified rate.
    /// </summary>
    /// <param name="rate">The rate to compare with.</param>
    /// <returns><see langword="true" /> if this rate exceeds the specified rate; otherwise, <see langword="false" />.</returns>
    public bool Exceeds(Rate rate)
    {
        return this > rate;
    }

    /// <summary>
    ///     Calculates the ratio of this rate to the specified rate.
    /// </summary>
    /// <param name="rate">The rate to compare with.</param>
    /// <returns>The ratio of this rate to the specified rate.</returns>
    public double RatioTo(Rate rate)
    {
        return CountPerSecond / rate.CountPerSecond;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Count} per {Duration.Humanize()}";
    }
}
