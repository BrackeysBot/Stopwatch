namespace Stopwatch.Configuration;

public sealed class BotConfiguration
{
    /// <summary>
    ///     Gets the interval, in seconds, at which the bot will update the slowmode rate limit for all channels.
    /// </summary>
    /// <value>The update interval.</value>
    public int UpdateInterval { get; set; } = 2000;
}
