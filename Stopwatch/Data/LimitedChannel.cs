namespace Stopwatch.Data;

/// <summary>
///     Represents a channel which has tracked automatic slowmode.
/// </summary>
internal sealed class LimitedChannel : IEquatable<LimitedChannel>
{
    /// <summary>
    ///     Gets or sets the slowmode activity window.
    /// </summary>
    /// <value>The slowmode activity window.</value>
    public double ActivityWindow { get; set; } = 10.0;

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    /// <value>The channel ID.</value>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the slowmode decay rate.
    /// </summary>
    /// <value>The slowmode decay rate.</value>
    public double DecayRate { get; set; } = 0.95;

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    /// <value>The guild ID.</value>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the slowmode rate limit threshold.
    /// </summary>
    /// <value>The slowmode rate limit threshold.</value>
    public double Threshold { get; set; } = 0.2;

    public static bool operator ==(LimitedChannel? left, LimitedChannel? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LimitedChannel? left, LimitedChannel? right)
    {
        return !Equals(left, right);
    }

    /// <inheritdoc />
    public bool Equals(LimitedChannel? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ChannelId == other.ChannelId && GuildId == other.GuildId;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is LimitedChannel other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // ReSharper disable NonReadonlyMemberInGetHashCode
        return HashCode.Combine(ChannelId, GuildId);
    }
}
