using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Stopwatch.Data;
using Stopwatch.Services;

namespace Stopwatch.Commands;

internal sealed class BucketsCommand : ApplicationCommandModule
{
    private readonly LimiterService _limiterService;
    private readonly MessageCountingService _countingService;

    public BucketsCommand(LimiterService limiterService, MessageCountingService countingService)
    {
        _limiterService = limiterService;
        _countingService = countingService;
    }

    [SlashCommand("buckets", "View the current bucket counts for a channel.", false)]
    [SlashRequireGuild]
    public async Task BucketsAsync(InteractionContext context,
        [Option("channel", "The channel whose buckets to retrieve.")]
        DiscordChannel? channel = null)
    {
        channel ??= context.Channel;

        if (!_limiterService.TryGetRate(channel, out Rate limit))
        {
            await context.CreateResponseAsync("Channel is not rate limited").ConfigureAwait(false);
            return;
        }

        var lines = new StringBuilder();
        lines.AppendLine($"Limit: {limit} ({limit.CountPerSecond * 10} per 10s)");
        lines.AppendLine("Buckets:   0s 10s 20s 30s 40s 50s");

        Rate[][] buckets = _countingService.GetBuckets(channel).Chunk(6).ToArray();

        for (var time = 0; time < buckets.Length; time++)
        {
            string formatted = string.Join(' ', buckets[time].Select(r => r.Count.ToString("D3")));
            lines.AppendLine($"{time:D7}m  {formatted}");
        }

        await context.CreateResponseAsync(Formatter.BlockCode(lines.ToString())).ConfigureAwait(false);
    }
}
