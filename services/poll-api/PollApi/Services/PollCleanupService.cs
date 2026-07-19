namespace PollApi.Services;

/// <summary>
/// Runs on a timer and closes any poll past its expiry, so the poll is durably Closed in the DB
/// rather than only looking inactive when read. Once closed it stops accepting votes and the
/// results page switches to its "final results" state.
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
            // Wait before the first pass, so a fast-starting integration test never triggers a sweep.
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
                // Swallow a one-off failure (e.g. a brief DB blip) so the loop keeps running.
                _logger.LogWarning(ex, "Poll cleanup pass failed; will retry next interval");
            }
        }
    }
}
