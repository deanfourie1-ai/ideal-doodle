using BcReleasePlanPortal.Ingest;
using BcReleasePlanPortal.Ingest.Configuration;
using Microsoft.Extensions.Options;

namespace BcReleasePlanPortal.Worker;

/// <summary>
/// Runs <see cref="RoadmapIngestService"/> once a day at the configured local time (design doc
/// §6: "Schedule: daily 06:00 CET"). Creates a fresh DI scope per run since
/// <see cref="RoadmapIngestService"/> and its EF Core store are scoped.
/// </summary>
public sealed class DailyIngestBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RoadmapIngestOptions> options,
    TimeProvider timeProvider,
    ILogger<DailyIngestBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            logger.LogInformation("Next roadmap ingest run in {Delay} (at {RunTime} {TimeZone})", delay, options.Value.DailyRunTime, options.Value.TimeZoneId);

            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var ingestService = scope.ServiceProvider.GetRequiredService<RoadmapIngestService>();

        try
        {
            await ingestService.RunAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Roadmap ingest run threw unexpectedly");
        }
    }

    private TimeSpan TimeUntilNextRun()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
        var runTime = TimeOnly.ParseExact(options.Value.DailyRunTime, "HH:mm");

        var nowLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        var todayRunLocal = new DateTimeOffset(nowLocal.Date + runTime.ToTimeSpan(), nowLocal.Offset);

        var nextRunLocal = todayRunLocal > nowLocal ? todayRunLocal : todayRunLocal.AddDays(1);
        return nextRunLocal - nowLocal;
    }
}
