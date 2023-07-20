using System.Collections.Immutable;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Hosting;
using Stopwatch.Configuration;
using Stopwatch.Data;
using Timer = System.Timers.Timer;

namespace Stopwatch.Services;

internal sealed class MessageCountingService : BackgroundService
{
    private const int PastBucketsSize = 30;
    private static readonly TimeSpan BucketDuration = TimeSpan.FromSeconds(10);

    private readonly DiscordClient _discordClient;
    private readonly ConfigurationService _configurationService;
    private readonly Timer _bucketTimer = new();
    private CountingBucket _currentBucket = new();
    private ImmutableList<ImmutableBucket> _pastBuckets = ImmutableList<ImmutableBucket>.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageCountingService" /> class.
    /// </summary>
    /// <param name="discordClient">The Discord client.</param>
    /// <param name="configurationService">The configuration service.</param>
    public MessageCountingService(DiscordClient discordClient, ConfigurationService configurationService)
    {
        _discordClient = discordClient;
        _configurationService = configurationService;

        _bucketTimer.Interval = BucketDuration.TotalMilliseconds;
    }

    public void ClearBuckets(DiscordChannel channel)
    {
        ClearBuckets(channel.Id);
    }

    public void ClearBuckets(ulong channelId)
    {
        _pastBuckets = _pastBuckets.Select(b => b.ClearCounts(channelId)).ToImmutableList();
    }

    public ImmutableList<Rate> GetBuckets(DiscordChannel channel)
    {
        return GetBuckets(channel.Id);
    }

    public ImmutableList<Rate> GetBuckets(ulong channelId)
    {
        IEnumerable<long> counts = _pastBuckets.Select(b => b.GetCount(channelId));
        return counts
            .Concat(Enumerable.Repeat(0L, PastBucketsSize))
            .Take(PastBucketsSize)
            .Select(c => Rate.Per(BucketDuration, c))
            .ToImmutableList();
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _discordClient.MessageCreated += OnMessageCreated;
        _bucketTimer.Elapsed += (_, _) => RotateBucket();
        _bucketTimer.Start();
        return Task.CompletedTask;
    }

    private void RotateBucket()
    {
        ImmutableBucket bucket = _currentBucket.Snapshot();
        _currentBucket = new CountingBucket();
        _pastBuckets = _pastBuckets.Take(PastBucketsSize - 1).Prepend(bucket).ToImmutableList();
    }

    private Task OnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
    {
        if (!_configurationService.TryGetGuildConfiguration(args.Guild, out GuildConfiguration? guildConfiguration))
        {
            guildConfiguration = new GuildConfiguration();
        }

        bool isBotMessage = args.Author.IsBot;

        if (guildConfiguration.CountBotMessages || !isBotMessage)
        {
            _currentBucket.Increment(args.Channel.Id);
        }

        return Task.CompletedTask;
    }
}
