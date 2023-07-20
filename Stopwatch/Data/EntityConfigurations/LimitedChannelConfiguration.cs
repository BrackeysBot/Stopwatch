using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Stopwatch.Data.EntityConfigurations;

/// <summary>
///     Represents a class which defines entity configuration for the <see cref="LimitedChannel" /> class.
/// </summary>
internal sealed class LimitedChannelConfiguration : IEntityTypeConfiguration<LimitedChannel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LimitedChannel> builder)
    {
        builder.ToTable(nameof(LimitedChannel));
        builder.HasKey(e => new { e.GuildId, e.ChannelId });

        builder.Property(e => e.GuildId);
        builder.Property(e => e.ChannelId);
        builder.Property(e => e.Count);
        builder.Property(e => e.Duration);
    }
}
