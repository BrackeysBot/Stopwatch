namespace Stopwatch.Data;

/// <summary>
///     Represents a channel which has tracked automatic slowmode.
/// </summary>
internal sealed class LimitedChannel : IEquatable<LimitedChannel>
{
    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    /// <value>The channel ID.</value>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    /// <value>The guild ID.</value>
    public ulong GuildId { get; set; }

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
