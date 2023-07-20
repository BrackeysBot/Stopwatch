using System.Collections.Immutable;

namespace Stopwatch.Data;

internal sealed class ImmutableBucket
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImmutableBucket" /> class.
    /// </summary>
    /// <param name="counts">The dictionary of channel IDs to message counts.</param>
    public ImmutableBucket(IDictionary<ulong, long> counts)
    {
        Counts = counts.ToImmutableDictionary();
    }

    /// <summary>
    ///     Gets a map of the channel IDs to the message counts.
    /// </summary>
    /// <value>The count map.</value>
    public ImmutableDictionary<ulong, long> Counts { get; }

    /// <summary>
    ///     Returns a new <see cref="ImmutableBucket" /> with the specified channel ID omitted.
    /// </summary>
    /// <param name="channelId">The channel ID to omit.</param>
    /// <returns>The new <see cref="ImmutableBucket" />.</returns>
    public ImmutableBucket ClearCounts(ulong channelId)
    {
        var newCounts = new Dictionary<ulong, long>(Counts);
        newCounts.Remove(channelId);
        return new ImmutableBucket(newCounts);
    }

    /// <summary>
    ///     Gets the message count for the specified channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The message count.</returns>
    public long GetCount(ulong channelId)
    {
        return Counts.TryGetValue(channelId, out long count) ? count : 0;
    }
}
