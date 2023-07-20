using System.Collections.Concurrent;

namespace Stopwatch.Data;

internal sealed class CountingBucket
{
    private readonly ConcurrentDictionary<ulong, long> _counts = new();

    /// <summary>
    ///     Increments the message count for the specified channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    public void Increment(ulong channelId)
    {
        _counts.AddOrUpdate(channelId, 1, (_, count) => count + 1);
    }

    /// <summary>
    ///     Returns a new <see cref="ImmutableBucket" /> representing the current state of the bucket.
    /// </summary>
    /// <returns>The new <see cref="ImmutableBucket" />.</returns>
    public ImmutableBucket Snapshot()
    {
        return new ImmutableBucket(_counts);
    }
}
