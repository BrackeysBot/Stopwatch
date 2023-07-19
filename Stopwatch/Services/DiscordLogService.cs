using System.Diagnostics.CodeAnalysis;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Stopwatch.Configuration;

namespace Stopwatch.Services;

/// <summary>
///     Represents a service which can send embeds to a log channel.
/// </summary>
internal sealed class DiscordLogService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly DiscordClient _discordClient;
    private readonly ConfigurationService _configurationService;
    private readonly Dictionary<DiscordGuild, DiscordChannel> _logChannels = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiscordLogService" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="discordClient">The Discord client.</param>
    /// <param name="configurationService">The configuration service.</param>
    public DiscordLogService(IConfiguration configuration, DiscordClient discordClient,
        ConfigurationService configurationService)
    {
        _configuration = configuration;
        _discordClient = discordClient;
        _configurationService = configurationService;
    }

    /// <summary>
    ///     Sends an embed to the log channel of the specified guild.
    /// </summary>
    /// <param name="guild">The guild whose log channel in which to post the embed.</param>
    /// <param name="embed">The embed to post.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="guild" /> or <paramref name="embed" /> is <see langword="null" />.
    /// </exception>
    public async Task LogAsync(DiscordGuild guild, DiscordEmbed embed)
    {
        if (guild is null)
        {
            throw new ArgumentNullException(nameof(guild));
        }

        if (embed is null)
        {
            throw new ArgumentNullException(nameof(embed));
        }

        if (_logChannels.TryGetValue(guild, out DiscordChannel? logChannel))
        {
            if (embed.Timestamp is null)
            {
                embed = new DiscordEmbedBuilder(embed).WithTimestamp(DateTimeOffset.UtcNow);
            }

            await logChannel.SendMessageAsync(embed).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets the log channel for a specified guild.
    /// </summary>
    /// <param name="guild">The guild whose log channel to retrieve.</param>
    /// <param name="channel">
    ///     When this method returns, contains the log channel; or <see langword="null" /> if no such channel is found.
    /// </param>
    /// <returns><see langword="true" /> if the log channel was successfully found; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="guild" /> is <see langword="null" />.</exception>
    public bool TryGetLogChannel(DiscordGuild guild, [NotNullWhen(true)] out DiscordChannel? channel)
    {
        if (guild is null)
        {
            throw new ArgumentNullException(nameof(guild));
        }

        if (!_configurationService.TryGetGuildConfiguration(guild, out GuildConfiguration? configuration))
        {
            channel = null;
            return false;
        }

        if (!_logChannels.TryGetValue(guild, out channel))
        {
            channel = guild.GetChannel(configuration.LogChannel);
            _logChannels.Add(guild, channel);
        }

        return channel is not null;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _discordClient.GuildAvailable += OnGuildAvailable;
        return Task.CompletedTask;
    }

    private async Task OnGuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
    {
        var logChannel = _configuration.GetSection(e.Guild.Id.ToString())?.GetSection("logChannel")?.Get<ulong>();
        if (!logChannel.HasValue)
        {
            return;
        }

        try
        {
            DiscordChannel? channel = await _discordClient.GetChannelAsync(logChannel.Value).ConfigureAwait(false);

            if (channel is not null)
            {
                _logChannels[e.Guild] = channel;
            }
        }
        catch
        {
            // ignored
        }
    }
}
