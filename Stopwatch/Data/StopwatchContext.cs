using Microsoft.EntityFrameworkCore;
using Stopwatch.Data.EntityConfigurations;

namespace Stopwatch.Data;

/// <summary>
///     Represents a session with the <c>stopwatch.db</c> database.
/// </summary>
internal sealed class StopwatchContext : DbContext
{
    /// <summary>
    ///     Gets the set of automatic slowmode channels.
    /// </summary>
    /// <value>The set of automatic slowmode channels.</value>
    public DbSet<LimitedChannel> LimitedChannels { get; internal set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseSqlite("Data Source='data/stopwatch.db'");
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new LimitedChannelConfiguration());
    }
}
