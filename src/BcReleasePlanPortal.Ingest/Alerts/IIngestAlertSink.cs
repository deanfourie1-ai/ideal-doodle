using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Ingest.Alerts;

/// <summary>
/// Design doc §6 step 6: "Any new breaking_change / deprecation, or any GA date movement on an
/// item already published in a customer plan, goes to a Teams webhook immediately."
/// </summary>
public interface IIngestAlertSink
{
    Task SendAsync(RoadmapItem item, IReadOnlyList<ChangeEvent> events, CancellationToken ct);
}
