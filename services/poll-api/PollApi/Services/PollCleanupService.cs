namespace PollApi.Services;

/// <summary>
/// Background service that periodically closes polls whose expiry has passed, so an expired
/// poll is durably Closed (not just computed-inactive) and stops accepting votes. The results
/// page shows a "closed — final results" banner once a poll is no longer active.
/// </summary>
public class PollCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollCleanupService> _logger;
    private readonly TimeSpan _interval;

    public PollCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<PollCleanupService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = config.GetValue("PollCleanup:IntervalSeconds", 60);
        _interval = TimeSpan.FromSeconds(seconds <= 0 ? 60 : seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay first so the service doesn't run during fast integration-test startup.
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var polls = scope.ServiceProvider.GetRequiredService<PollService>();
                var closed = await polls.CloseExpiredPollsAsync();
                if (closed > 0)
                    _logger.LogInformation("Auto-closed {Count} expired poll(s)", closed);
            }
            catch (Exception ex)
            {
                // Never let a transient failure kill the loop.
                _logger.LogWarning(ex, "Poll cleanup pass failed; will retry next interval");
            }
        }
    }
}
