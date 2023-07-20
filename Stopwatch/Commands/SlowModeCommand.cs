using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Humanizer;
using Stopwatch.Configuration;
using Stopwatch.Services;
using X10D.DSharpPlus;
using X10D.Time;

namespace Stopwatch.Commands;

/// <summary>
///     Represents a class which implements the <c>slowmode</c> command.
/// </summary>
internal sealed partial class SlowModeCommand : ApplicationCommandModule
{
    private static readonly Regex RateRegex = GetRateRegex();
    private readonly SlowModeService _slowModeService;
    private readonly DiscordLogService _logService;
    private readonly ConfigurationService _configurationService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlowModeCommand" /> class.
    /// </summary>
    /// <param name="slowModeService">The slowmode service.</param>
    /// <param name="logService">The log service.</param>
    /// <param name="configurationService">The configuration service.</param>
    public SlowModeCommand(SlowModeService slowModeService,
        DiscordLogService logService,
        ConfigurationService configurationService)
    {
        _slowModeService = slowModeService;
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
        TimeSpan? slowmode = null;
        var automatic = false;

        if (int.TryParse(timeRaw, out int seconds))
        {
            slowmode = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 21600)); // 21600s = 6h. Discord-imposed maximum
            _slowModeService.SetAutomaticSlowMode(channel, false, 0, 0, 0);
        }
        else if (TimeSpanParser.TryParse(timeRaw, out TimeSpan duration))
        {
            slowmode = duration;
            _slowModeService.SetAutomaticSlowMode(channel, false, 0, 0, 0);
        }
        else if (string.Equals(timeRaw, "off", StringComparison.OrdinalIgnoreCase))
        {
            slowmode = TimeSpan.Zero; // this is really just a QoL feature. since we accept a string then... why not?
            _slowModeService.SetAutomaticSlowMode(channel, false, 0, 0, 0);
        }
        else if (string.Equals(timeRaw, "auto", StringComparison.OrdinalIgnoreCase))
        {
            slowmode = null;
            automatic = true;

            if (!_configurationService.TryGetGuildConfiguration(context.Guild, out GuildConfiguration? configuration))
            {
                configuration = new GuildConfiguration();
            }

            _slowModeService.SetAutomaticSlowMode(channel, true, configuration.DefaultThreshold,
                configuration.DefaultActivityWindow, configuration.DefaultDecayRate);
        }
        else if (TryGetRate(context.Guild, timeRaw, out double threshold, out double window, out double decay))
        {
            automatic = true;
            slowmode = null;
            Console.WriteLine($"Threshold: {threshold}, Window: {window}, Decay: {decay}");
            _slowModeService.SetAutomaticSlowMode(channel, true, threshold, window, decay);
        }
        else
        {
            automatic = !slowmode.HasValue;
            _slowModeService.SetAutomaticSlowMode(channel, automatic, -1, -1, -1);
        }

        var embed = new DiscordEmbedBuilder();
        embed.AddField("Channel", channel.Mention, true);
        embed.AddField("Staff Member", context.User.Mention, true);

        if (automatic)
        {
            embed.WithColor(DiscordColor.Orange);
            embed.WithTitle("Slowmode Enabled");
            embed.AddField("Slowmode", "Automatic", true);
            embed.AddField("Threshold", _slowModeService.GetThreshold(channel), true);
            embed.AddField("Activity Window", _slowModeService.GetActivityWindow(channel), true);
            embed.AddField("Decay Rate", _slowModeService.GetDecayRate(channel), true);
        }
        else
        {
            _slowModeService.SetAutomaticSlowMode(channel, false, 0, 0, 0);
            TimeSpan duration = slowmode!.Value;

            embed.WithColor(duration > TimeSpan.Zero ? DiscordColor.Orange : DiscordColor.Green);
            embed.WithTitle($"Slowmode {(duration > TimeSpan.Zero ? "Enabled" : "Disabled")}");
            embed.AddFieldIf(duration > TimeSpan.Zero, "Slowmode", duration.Humanize(), true);
            await _slowModeService.SetSlowModeAsync(channel, duration);
        }

        await context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed.Build())).ConfigureAwait(false);
        await _logService.LogAsync(context.Guild, embed).ConfigureAwait(false);
    }

    private bool TryGetRate(DiscordGuild guild, string input, out double threshold, out double window, out double decay)
    {
        if (!_configurationService.TryGetGuildConfiguration(guild, out GuildConfiguration? configuration))
        {
            configuration = new GuildConfiguration();
        }

        Match match = RateRegex.Match(input);
        if (!match.Success)
        {
            threshold = configuration.DefaultThreshold;
            window = configuration.DefaultActivityWindow;
            decay = configuration.DefaultDecayRate;
            return false;
        }

        GroupCollection groups = match.Groups;
        if (!double.TryParse(groups[1].Value, out threshold))
        {
            threshold = configuration.DefaultThreshold;
        }

        if (!double.TryParse(groups[2].Value, out window))
        {
            window = configuration.DefaultActivityWindow;
        }

        if (groups.Count < 3 || !groups[3].Success || !double.TryParse(groups[3].Value, out decay))
        {
            decay = configuration.DefaultDecayRate;
        }

        return true;
    }

    [GeneratedRegex(@"^((?:\d*\.)?\d+)/((?:\d*\.)?\d+)(?:/((?:\d*\.)?\d+))?$", RegexOptions.Compiled)]
    private static partial Regex GetRateRegex();
}
