using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stopwatch.Data;

namespace Stopwatch.Services;

/// <summary>
///     Represents a service which ensures the database is created.
/// </summary>
internal sealed class DatabaseService : BackgroundService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabaseService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeFactory">The service scope factory.</param>
    public DatabaseService(ILogger<DatabaseService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<StopwatchContext>();

        _logger.LogInformation("Creating database");
        await context.Database.EnsureCreatedAsync(stoppingToken);
    }
}
