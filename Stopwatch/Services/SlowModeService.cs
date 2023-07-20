using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Timers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stopwatch.Configuration;
using Stopwatch.Data;
using Timer = System.Timers.Timer;

namespace Stopwatch.Services;

/// <summary>
///     Represents a service which manages automatic slowmode.
/// </summary>
internal sealed class SlowModeService : BackgroundService
{
    private const int MaxSlowmode = 21600; // 6h, Discord's max

    private readonly ILogger<SlowModeService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConfigurationService _configurationService;
    private readonly DiscordClient _discordClient;
    private readonly List<LimitedChannel> _activeChannels = new();
    private readonly ConcurrentDictionary<DiscordChannel, TimeSpan> _channelRates = new();
    private readonly ConcurrentDictionary<ulong, double> _activityWindows = new();
    private readonly ConcurrentDictionary<ulong, double> _decayRates = new();
    private readonly ConcurrentDictionary<ulong, double> _thresholds = new();
    private readonly ConcurrentDictionary<DiscordChannel, List<DateTimeOffset>> _messageTimestamps = new();
    private readonly Timer _updateTimer = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlowModeService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="discordClient">The Discord client.</param>
    public SlowModeService(ILogger<SlowModeService> logger,
        IServiceScopeFactory scopeFactory,
        ConfigurationService configurationService,
        DiscordClient discordClient)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configurationService = configurationService;
        _discordClient = discordClient;

        _updateTimer.Enabled = false;
        _updateTimer.Interval = configurationService.BotConfiguration.UpdateInterval;
        _updateTimer.Elapsed += UpdateTimerOnElapsed;
    }

    /// <summary>
    ///     Gets the activity window for the specified channel.
    /// </summary>
    /// <param name="channel">The channel whose activity window to retrieve.</param>
    /// <returns>The activity window for the specified channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public double GetActivityWindow(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (_activityWindows.TryGetValue(channel.Id, out double activityWindow))
        {
            return activityWindow;
        }

        if (!_configurationService.TryGetGuildConfiguration(channel.Guild, out GuildConfiguration? configuration))
        {
            configuration = new GuildConfiguration();
        }

        return configuration.DefaultActivityWindow;
    }

    /// <summary>
    ///     Gets the decay rate for the specified channel.
    /// </summary>
    /// <param name="channel">The channel whose decay rate to retrieve.</param>
    /// <returns>The decay rate for the specified channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public double GetDecayRate(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (_decayRates.TryGetValue(channel.Id, out double decayRate))
        {
            return decayRate;
        }

        if (!_configurationService.TryGetGuildConfiguration(channel.Guild, out GuildConfiguration? configuration))
        {
            configuration = new GuildConfiguration();
        }

        return configuration.DefaultDecayRate;
    }

    /// <summary>
    ///     Gets the threshold for the specified channel.
    /// </summary>
    /// <param name="channel">The channel whose threshold to retrieve.</param>
    /// <returns>The threshold for the specified channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public double GetThreshold(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (_thresholds.TryGetValue(channel.Id, out double threshold))
        {
            return threshold;
        }

        if (!_configurationService.TryGetGuildConfiguration(channel.Guild, out GuildConfiguration? configuration))
        {
            configuration = new GuildConfiguration();
        }

        return configuration.DefaultThreshold;
    }

    /// <summary>
    ///     Sets the slowmode of a channel
    /// </summary>
    /// <param name="channel">The channel whose slowmode to set.</param>
    /// <param name="duration">The duration of the slowmode.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     <para><paramref name="duration" /> is less than 0 or greater than 6 hours.</para>
    ///     -or-
    ///     <para><paramref name="channel" /> is not a guild channel.</para>
    ///     -or-
    ///     <para><paramref name="channel" /> is not a valid channel type.</para>
    /// </exception>
    public async Task SetSlowModeAsync(DiscordChannel channel, TimeSpan duration)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentException("Duration cannot be less than zero.", nameof(duration));
        }

        if (duration > TimeSpan.FromHours(6))
        {
            throw new ArgumentException("Duration cannot be greater than 6 hours.", nameof(duration));
        }

        if (channel.Guild is null)
        {
            throw new ArgumentException("Channel is not in a guild.", nameof(channel));
        }

        if (channel.Type is ChannelType.Voice or ChannelType.Stage or ChannelType.Category)
        {
            throw new ArgumentException("Cannot set slowmode for voice or stage channel.", nameof(channel));
        }

        var slowmode = (int)duration.TotalSeconds;

        if ((channel.PerUserRateLimit ?? 0) == slowmode)
        {
            _logger.LogDebug("Slowmode for {Channel} is already {Duration}", channel, duration);
            return;
        }

        _logger.LogInformation("Setting slowmode for {Channel} to {Duration}", channel, duration);
        await channel.ModifyAsync(model => model.PerUserRateLimit = slowmode);
    }

    /// <summary>
    ///     Enables or disables automatic slowmode for the specified channel.
    /// </summary>
    /// <param name="channel">The channel whose slowmode to set.</param>
    /// <param name="enable">
    ///     <see langword="true" /> to enable automatic slowmode for <paramref name="channel" />; otherwise,
    ///     <see langword="false" />.
    /// </param>
    /// <param name="threshold">The threshold for automatic slowmode.</param>
    /// <param name="activityWindow">The activity window for automatic slowmode.</param>
    /// <param name="decayRate">The decay rate for automatic slowmode.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="channel" /> is not a valid channel type, or is not a guild channel.
    /// </exception>
    public void SetAutomaticSlowMode(DiscordChannel channel,
        bool enable,
        double threshold,
        double activityWindow,
        double decayRate)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (channel.Guild is null)
        {
            throw new ArgumentException("Channel is not in a guild.", nameof(channel));
        }

        if (channel.Type is ChannelType.Voice or ChannelType.Stage or ChannelType.Category)
        {
            throw new ArgumentException("Cannot set slowmode for voice or stage channel.", nameof(channel));
        }

        if (enable)
        {
            if (TryGetAutomaticSlowModeChannel(channel, out LimitedChannel? limitedChannel))
            {
                _logger.LogInformation("Updating automatic slowmode rate for {Channel}", channel);
                RemoveAutomaticSlowModeChannel(limitedChannel);
                _activeChannels.Remove(limitedChannel);
            }
            else
            {
                _logger.LogInformation("Enabling automatic slowmode for {Channel}", channel);
            }

            LimitedChannel limited = CreateAutomaticSlowModeChannel(channel, threshold, activityWindow, decayRate);
            _activeChannels.Add(limited);
        }
        else if (TryGetAutomaticSlowModeChannel(channel, out LimitedChannel? limitedChannel))
        {
            _logger.LogInformation("Disabling automatic slowmode for {Channel}", channel);
            RemoveAutomaticSlowModeChannel(limitedChannel);
            _activeChannels.Remove(limitedChannel);
            _messageTimestamps.TryRemove(channel, out _);
        }
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _discordClient.GuildAvailable -= DiscordClientOnGuildAvailable;
        _discordClient.MessageCreated -= DiscordClientOnMessageCreated;
        _updateTimer.Stop();
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _discordClient.GuildAvailable += DiscordClientOnGuildAvailable;
        _discordClient.MessageCreated += DiscordClientOnMessageCreated;
        _updateTimer.Start();
        return Task.CompletedTask;
    }

    private double CalculateActivityRate(DiscordChannel channel)
    {
        double activityWindow = GetActivityWindow(channel);
        DateTime currentDateTime = DateTime.Now;
        DateTime activityWindowStart = currentDateTime - TimeSpan.FromSeconds(activityWindow);

        if (!_messageTimestamps.TryGetValue(channel, out List<DateTimeOffset>? messageTimestamps))
        {
            return 0.0;
        }

        int messageCount = messageTimestamps.RemoveAll(timestamp => timestamp < activityWindowStart);
        return messageCount / activityWindow;
    }

    private LimitedChannel CreateAutomaticSlowModeChannel(DiscordChannel channel,
        double threshold,
        double activityWindow,
        double decayRate)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (TryGetAutomaticSlowModeChannel(channel, out LimitedChannel? automaticChannel))
        {
            return automaticChannel;
        }

        _thresholds[channel.Id] = threshold;
        _activityWindows[channel.Id] = activityWindow;
        _decayRates[channel.Id] = decayRate;

        using IServiceScope scope = _scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<StopwatchContext>();
        EntityEntry<LimitedChannel> entity = context.LimitedChannels.Add(new LimitedChannel
        {
            GuildId = channel.Guild.Id,
            ChannelId = channel.Id,
            Threshold = threshold,
            ActivityWindow = activityWindow,
            DecayRate = decayRate
        });
        context.SaveChanges();
        return entity.Entity;
    }

    private void RemoveAutomaticSlowModeChannel(LimitedChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        _thresholds.TryRemove(channel.ChannelId, out _);
        _activityWindows.TryRemove(channel.ChannelId, out _);
        _decayRates.TryRemove(channel.ChannelId, out _);

        using IServiceScope scope = _scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<StopwatchContext>();
        context.Remove(channel);
        context.SaveChanges();
    }

    private bool TryGetAutomaticSlowModeChannel(DiscordChannel channel, [NotNullWhen(true)] out LimitedChannel? mapped)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        foreach (LimitedChannel activeChannel in _activeChannels)
        {
            if (activeChannel.GuildId == channel.Guild.Id && activeChannel.ChannelId == channel.Id)
            {
                mapped = activeChannel;
                return true;
            }
        }

        mapped = null;
        return false;
    }

    private async void UpdateTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        var tasks = new List<Task>();

        foreach (LimitedChannel channel in _activeChannels)
        {
            DiscordGuild guild = await _discordClient.GetGuildAsync(channel.GuildId);
            DiscordChannel discordChannel = guild.GetChannel(channel.ChannelId);

            double activityRate = CalculateActivityRate(discordChannel);
            if (activityRate >= GetThreshold(discordChannel))
            {
                _channelRates[discordChannel] = TimeSpan.FromSeconds(Math.Min(activityRate * 10, MaxSlowmode));
            }
            else
            {
                _channelRates[discordChannel] =
                    TimeSpan.FromSeconds(Math.Min(activityRate * GetDecayRate(discordChannel), MaxSlowmode));
            }

            tasks.Add(SetSlowModeAsync(discordChannel, _channelRates[discordChannel]));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private Task DiscordClientOnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
    {
        if (e.Guild is null ||
            !_configurationService.TryGetGuildConfiguration(e.Guild, out GuildConfiguration? configuration))
        {
            return Task.CompletedTask;
        }

        if (e.Author == _discordClient.CurrentUser)
        {
            return Task.CompletedTask;
        }

        var author = (DiscordMember)e.Author;
        if (author.IsBot && !configuration.CountBotMessages)
        {
            return Task.CompletedTask;
        }

        // cache to prevent repeated property access in lambda below
        ulong[] ignoredRoleIds = configuration.IgnoredRoleIds;
        if (author.Roles.Any(r => ignoredRoleIds.Contains(r.Id)))
        {
            return Task.CompletedTask;
        }

        _messageTimestamps.AddOrUpdate(e.Channel, new List<DateTimeOffset> { DateTimeOffset.Now },
            (_, timestamps) =>
            {
                timestamps.Add(DateTimeOffset.Now);
                return timestamps;
            });
        return Task.CompletedTask;
    }

    private async Task DiscordClientOnGuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<StopwatchContext>();
        foreach (LimitedChannel channel in context.LimitedChannels.Where(c => c.GuildId == e.Guild.Id))
        {
            _activeChannels.Add(channel);
        }
    }
}
