using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Humanizer;
using Stopwatch.Services;
using X10D.DSharpPlus;
using X10D.Time;

namespace Stopwatch.Commands;

/// <summary>
///     Represents a class which implements the <c>slowmode</c> command.
/// </summary>
internal sealed class SlowModeCommand : ApplicationCommandModule
{
    private readonly SlowModeService _slowModeService;
    private readonly DiscordLogService _logService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlowModeCommand" /> class.
    /// </summary>
    /// <param name="slowModeService">The slowmode service.</param>
    /// <param name="logService">The log service.</param>
    public SlowModeCommand(SlowModeService slowModeService, DiscordLogService logService)
    {
        _slowModeService = slowModeService;
        _logService = logService;
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

        TimeSpan? slowmode = null;

        if (int.TryParse(timeRaw, out int seconds))
        {
            slowmode = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 21600)); // 21600s = 6h. Discord-imposed maximum
        }
        else if (TimeSpanParser.TryParse(timeRaw, out TimeSpan duration))
        {
            slowmode = duration;
        }
        else if (string.Equals(timeRaw, "off", StringComparison.OrdinalIgnoreCase))
        {
            slowmode = TimeSpan.Zero; // this is really just a QoL feature. since we accept a string then... why not?
        }
        else if (string.Equals(timeRaw, "auto", StringComparison.OrdinalIgnoreCase))
        {
            slowmode = null;
        }

        channel ??= context.Channel;

        bool automatic = !slowmode.HasValue;
        _slowModeService.SetAutomaticSlowMode(channel, automatic);

        var embed = new DiscordEmbedBuilder();
        if (automatic)
        {
            embed.WithColor(DiscordColor.Orange);
            embed.WithTitle("Slowmode Enabled");
            embed.AddField("Channel", channel.Mention, true);
            embed.AddField("Slowmode", "Automatic", true);
        }
        else
        {
            TimeSpan duration = slowmode!.Value;

            embed.WithColor(duration > TimeSpan.Zero ? DiscordColor.Orange : DiscordColor.Green);
            embed.WithTitle($"Slowmode {(duration > TimeSpan.Zero ? "Enabled" : "Disabled")}");
            embed.AddField("Channel", channel.Mention, true);
            embed.AddFieldIf(duration > TimeSpan.Zero, "Slowmode", duration.Humanize(), true);
            await _slowModeService.SetSlowModeAsync(channel, duration);
        }

        await context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed.Build())).ConfigureAwait(false);

        embed.AddField("Staff Member", context.User.Mention, true);
        await _logService.LogAsync(context.Guild, embed).ConfigureAwait(false);
    }
}
