using System.Net.Http.Json;
using BcReleasePlanPortal.Domain;
using Microsoft.Extensions.Logging;

namespace BcReleasePlanPortal.Ingest.Alerts;

/// <summary>
/// Posts an "Incoming Webhook"-style message card to a Teams channel webhook URL. Design doc
/// §6 step 6. If no webhook URL is configured, use <see cref="NullAlertSink"/> instead — this
/// class assumes it's only constructed when a URL is present.
/// </summary>
public sealed class TeamsWebhookAlertSink(HttpClient httpClient, ILogger<TeamsWebhookAlertSink> logger) : IIngestAlertSink
{
    public async Task SendAsync(RoadmapItem item, IReadOnlyList<ChangeEvent> events, CancellationToken ct)
    {
        var summary = $"{item.ChangeType}: {item.Title}";
        var facts = events
            .Select(e => $"**{e.Field}**: {e.OldValue ?? "(none)"} → {e.NewValue ?? "(none)"}")
            .ToList();

        var payload = new
        {
            title = $"[{item.Product}] {summary}",
            text = string.Join("\n\n", facts.Prepend(item.Url ?? string.Empty).Where(s => s.Length > 0)),
        };

        var response = await httpClient.PostAsJsonAsync(string.Empty, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Teams webhook alert for roadmap item {ExternalId} failed with status {Status}", item.ExternalId, response.StatusCode);
        }
    }
}

/// <summary>No-op alert sink used when no Teams webhook URL is configured — logs instead of posting.</summary>
public sealed class NullAlertSink(ILogger<NullAlertSink> logger) : IIngestAlertSink
{
    public Task SendAsync(RoadmapItem item, IReadOnlyList<ChangeEvent> events, CancellationToken ct)
    {
        logger.LogInformation(
            "Ingest alert (no Teams webhook configured) for {ExternalId} \"{Title}\": {Fields}",
            item.ExternalId,
            item.Title,
            string.Join(", ", events.Select(e => e.Field)));
        return Task.CompletedTask;
    }
}
