namespace Stopwatch.Configuration;

/// <summary>
///     Represents a guild configuration.
/// </summary>
internal sealed class GuildConfiguration
{
    /// <summary>
    ///     Gets or sets a value indicating whether bot messages will be counted.
    /// </summary>
    /// <value><see langword="true" /> to count bot messages; otherwise, <see langword="false" />.</value>
    public bool CountBotMessages { get; set; } = false;

    /// <summary>
    ///     Gets or sets the default rate count threshold.
    /// </summary>
    /// <value>The default rate count threshold.</value>
    public long DefaultCount { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the default rate duration window.
    /// </summary>
    /// <value>The default rate duration window.</value>
    public double DefaultDuration { get; set; } = 10.0;

    /// <summary>
    ///     Gets or sets the ID of the log channel.
    /// </summary>
    public ulong LogChannel { get; set; }

    /// <summary>
    ///     Gets or sets the set of role IDs which will be ignored in the count.
    /// </summary>
    /// <value>The ignored role IDs.</value>
    public ulong[] IgnoredRoleIds { get; set; } = Array.Empty<ulong>();
}
