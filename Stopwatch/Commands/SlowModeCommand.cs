using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Humanizer;
using Stopwatch.Configuration;
using Stopwatch.Data;
using Stopwatch.Services;
using X10D.Time;

namespace Stopwatch.Commands;

/// <summary>
///     Represents a class which implements the <c>slowmode</c> command.
/// </summary>
internal sealed partial class SlowModeCommand : ApplicationCommandModule
{
    private static readonly Regex RateRegex = GetRateRegex();
    private readonly LimiterService _limiterService;
    private readonly DiscordLogService _logService;
    private readonly ConfigurationService _configurationService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlowModeCommand" /> class.
    /// </summary>
    /// <param name="limiterService">The limiter service.</param>
    /// <param name="logService">The log service.</param>
    /// <param name="configurationService">The configuration service.</param>
    public SlowModeCommand(LimiterService limiterService,
        DiscordLogService logService,
        ConfigurationService configurationService)
    {
        _limiterService = limiterService;
        _logService = logService;
        _configurationService = configurationService;
    }

    [SlashCommand("slowmode", "Sets the slowmode for this channel, or a specific channel.", false)]
    [SlashRequireGuild]
    public async Task SlowModeAsync(InteractionContext context,
        [Option("time", "The new slowmode duration. Set to 'auto' for automatic slowmode.")]
        string timeRaw,
        [Option("channel", "The channel to apply the slowmode. Defaults to the current channel")]
        DiscordChannel? channel = null
    )
    {
        await context.DeferAsync(true).ConfigureAwait(false);
        channel ??= context.Channel;

        var embed = new DiscordEmbedBuilder();
        var log = true;

        if (int.TryParse(timeRaw, out int seconds))
        {
            embed = seconds <= 0
                ? DisableSlowMode(context, channel)
                : SetStaticSlowMode(context, channel, TimeSpan.FromSeconds(seconds));
        }
        else if (TimeSpanParser.TryParse(timeRaw, out TimeSpan timeSpan))
        {
            embed = timeSpan == TimeSpan.Zero
                ? DisableSlowMode(context, channel)
                : SetStaticSlowMode(context, channel, timeSpan);
        }
        else if (string.Equals(timeRaw, "off", StringComparison.OrdinalIgnoreCase))
        {
            embed = DisableSlowMode(context, channel);
        }
        else if (string.Equals(timeRaw, "auto", StringComparison.OrdinalIgnoreCase))
        {
            embed = SetRatedSlowMode(context, channel, _limiterService.GetDefaultRate(context.Guild));
        }
        else if (TryGetRate(context.Guild, timeRaw, out long count, out double duration))
        {
            embed = SetRatedSlowMode(context, channel, Rate.Per(TimeSpan.FromSeconds(duration), count));
        }
        else
        {
            embed.WithTitle("Invalid Input");
            embed.WithColor(DiscordColor.Red);
            embed.WithDescription($"Input `{timeRaw}` was not recognized as a valid time duration or ratio.");
            await context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed.Build())).ConfigureAwait(false);
            log = false;
        }

        await context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed.Build())).ConfigureAwait(false);
        if (log)
        {
            await _logService.LogAsync(context.Guild, embed).ConfigureAwait(false);
        }
    }

    private static DiscordEmbedBuilder GetDefaultEmbed(BaseContext context, DiscordChannel channel)
    {
        var embed = new DiscordEmbedBuilder();
        embed.WithColor(DiscordColor.Orange);
        embed.WithTitle("Slowmode Enabled");
        embed.AddField("Channel", channel.Mention, true);
        embed.AddField("Staff Member", context.User.Mention, true);
        return embed;
    }

    private DiscordEmbedBuilder DisableSlowMode(BaseContext context, DiscordChannel channel)
    {
        _limiterService.RemoveLimiter(channel);

        return GetDefaultEmbed(context, channel).WithColor(DiscordColor.Green).WithTitle("Slowmode Disabled");
    }

    private DiscordEmbedBuilder SetRatedSlowMode(BaseContext context, DiscordChannel channel, Rate rate)
    {
        _limiterService.AddLimiter(channel, rate);

        return GetDefaultEmbed(context, channel).AddField("Slowmode", $"Automatic ({rate})", true);
    }

    private DiscordEmbedBuilder SetStaticSlowMode(BaseContext context, DiscordChannel channel, TimeSpan duration)
    {
        var seconds = (int)Math.Clamp(duration.TotalSeconds, 0, 21600); // 21600s = 6h. Discord-imposed maximum

        _limiterService.RemoveLimiter(channel);
        _limiterService.UpdateSlowMode(channel, (int)duration.TotalSeconds);

        return GetDefaultEmbed(context, channel).AddField("Slowmode", $"{duration.Humanize()} ({seconds}s)", true);
    }

    private bool TryGetRate(DiscordGuild guild, string input, out long count, out double duration)
    {
        if (!_configurationService.TryGetGuildConfiguration(guild, out GuildConfiguration? configuration))
        {
            configuration = new GuildConfiguration();
        }

        Match match = RateRegex.Match(input);
        if (!match.Success)
        {
            count = configuration.DefaultCount;
            duration = configuration.DefaultDuration;
            return false;
        }

        GroupCollection groups = match.Groups;
        if (!long.TryParse(groups[1].Value, out count))
        {
            count = configuration.DefaultCount;
        }

        if (TimeSpanParser.TryParse(groups[2].Value, out TimeSpan span))
        {
            duration = span.TotalSeconds;
        }
        else if (!double.TryParse(groups[2].Value, out duration))
        {
            duration = configuration.DefaultDuration;
        }

        return true;
    }

    [GeneratedRegex(@"^(\d+)/(.*?)$", RegexOptions.Compiled)]
    private static partial Regex GetRateRegex();
}
