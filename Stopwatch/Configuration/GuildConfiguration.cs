﻿namespace Stopwatch.Configuration;

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
    ///     Gets or sets the ID of the log channel.
    /// </summary>
    public ulong LogChannel { get; set; }

    /// <summary>
    ///     Gets or sets the set of role IDs which will be ignored in the count.
    /// </summary>
    /// <value>The ignored role IDs.</value>
    public ulong[] IgnoredRoleIds { get; set; } = Array.Empty<ulong>();
}
