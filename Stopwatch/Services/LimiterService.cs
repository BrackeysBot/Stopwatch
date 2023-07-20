using System.Collections.Concurrent;
using System.Timers;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stopwatch.Configuration;
using Stopwatch.Data;
using Timer = System.Timers.Timer;

namespace Stopwatch.Services;

internal sealed class LimiterService : BackgroundService
{
    private readonly ILogger<LimiterService> _logger;
    private readonly DiscordClient _discordClient;
    private readonly IDbContextFactory<StopwatchContext> _dbContextFactory;
    private readonly ConfigurationService _configurationService;
    private readonly MessageCountingService _messageCountingService;

    private readonly Timer _updateTimer;
    private readonly ConcurrentDictionary<ulong, Rate> _limits = new();
    private readonly ConcurrentDictionary<ulong, int> _slowmodes = new();

    private readonly List<LimitedChannel> _limitedChannels = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="LimiterService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="discordClient">The Discord client.</param>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="messageCountingService">The message counting service.</param>
    public LimiterService(ILogger<LimiterService> logger,
        DiscordClient discordClient,
        IDbContextFactory<StopwatchContext> dbContextFactory,
        ConfigurationService configurationService,
        MessageCountingService messageCountingService)
    {
        _logger = logger;
        _discordClient = discordClient;
        _dbContextFactory = dbContextFactory;
        _configurationService = configurationService;
        _messageCountingService = messageCountingService;
        _updateTimer = new Timer(2000);
    }

    /// <summary>
    ///     Adds a limiter for the specified channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="rate">The rate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public void AddLimiter(DiscordChannel channel, Rate rate)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        RemoveLimiter(channel);
        _limits[channel.Id] = rate;

        using StopwatchContext context = _dbContextFactory.CreateDbContext();
        EntityEntry<LimitedChannel> entry = context.LimitedChannels.Add(new LimitedChannel
        {
            GuildId = channel.Guild.Id,
            ChannelId = channel.Id,
            Count = rate.Count,
            Duration = rate.Duration.TotalSeconds
        });
        context.SaveChanges();
        _limitedChannels.Add(entry.Entity);
    }

    /// <summary>
    ///     Gets the default rate for the specified guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <returns>The default rate.</returns>
    public Rate GetDefaultRate(DiscordGuild guild)
    {
        if (guild is null)
        {
            throw new ArgumentNullException(nameof(guild));
        }

        if (!_configurationService.TryGetGuildConfiguration(guild, out GuildConfiguration? guildConfiguration))
        {
            guildConfiguration = new GuildConfiguration();
        }

        return Rate.Per(TimeSpan.FromSeconds(guildConfiguration.DefaultDuration), guildConfiguration.DefaultCount);
    }

    /// <summary>
    ///     Removes the limiter for the specified channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public void RemoveLimiter(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        UpdateSlowMode(channel, 0);
        _limits.TryRemove(channel.Id, out _);

        LimitedChannel? limitedChannel = _limitedChannels.Find(c => c.GuildId == channel.Guild.Id && c.ChannelId == channel.Id);
        if (limitedChannel is null)
        {
            return;
        }

        using StopwatchContext context = _dbContextFactory.CreateDbContext();
        context.LimitedChannels.Remove(limitedChannel);
        context.SaveChanges();
        _limitedChannels.Remove(limitedChannel);
        _messageCountingService.ClearBuckets(channel);
    }

    public bool TryGetRate(DiscordChannel channel, out Rate rate)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        return _limits.TryGetValue(channel.Id, out rate);
    }

    /// <summary>
    ///     Updates the slowmode for the specified channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="seconds">The new slowmode duration.</param>
    public void UpdateSlowMode(ulong channelId, int seconds)
    {
        _discordClient.GetChannelAsync(channelId).ContinueWith(t => UpdateSlowMode(t.Result, seconds));
    }

    /// <summary>
    ///     Updates the slowmode for the specified channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="seconds">The new slowmode duration.</param>
    public void UpdateSlowMode(DiscordChannel channel, int seconds)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (_slowmodes.TryGetValue(channel.Id, out int current) && current == seconds)
        {
            _logger.LogDebug("Slowmode for {Channel} is already {Seconds} seconds", channel, seconds);
            return;
        }

        _logger.LogDebug("Updating slowmode for {Channel} to {Seconds} seconds", channel, seconds);
        _slowmodes[channel.Id] = seconds;
        channel.ModifyAsync(c => c.PerUserRateLimit = seconds);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _updateTimer.Stop();
        _updateTimer.Elapsed -= OnUpdateTimerElapsed;

        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _updateTimer.Elapsed += OnUpdateTimerElapsed;
        _updateTimer.Start();

        FetchFromDatabase();
        return Task.CompletedTask;
    }

    private void UpdateSlowMode()
    {
        foreach (ulong channelId in _limits.Keys)
        {
            UpdateSlowMode(channelId);
        }
    }

    private void UpdateSlowMode(DiscordChannel channel)
    {
        UpdateSlowMode(channel.Id);
    }

    private void UpdateSlowMode(ulong channelId)
    {
        if (!_limits.TryGetValue(channelId, out Rate limit))
        {
            return;
        }

        var slowmode = (int)_messageCountingService.GetBuckets(channelId)
            .Select((b, i) => b.Exceeds(limit) ? b.RatioTo(limit) * (30.0 - i) / 10.0 : 0.0)
            .Sum();

        UpdateSlowMode(channelId, slowmode);
    }

    private void OnUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        UpdateSlowMode();
    }

    private void FetchFromDatabase()
    {
        using StopwatchContext context = _dbContextFactory.CreateDbContext();
        _limitedChannels.AddRange(context.LimitedChannels);
        foreach (LimitedChannel limitedChannel in _limitedChannels)
        {
            _limits[limitedChannel.ChannelId] = Rate.Per(TimeSpan.FromSeconds(limitedChannel.Duration), limitedChannel.Count);
        }

        _logger.LogInformation("Loaded {Count} limiters from the database", _limitedChannels.Count);
    }
}
