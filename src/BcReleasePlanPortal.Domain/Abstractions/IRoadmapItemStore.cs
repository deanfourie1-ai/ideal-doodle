namespace BcReleasePlanPortal.Domain.Abstractions;

/// <summary>
/// Persistence seam between the Ingest pipeline and the Data layer's EF Core store, so Ingest
/// can depend on Domain only (no EF Core reference). Implemented by
/// <c>BcReleasePlanPortal.Data.RoadmapItemStore</c>.
/// </summary>
public interface IRoadmapItemStore
{
    Task<RoadmapItem?> FindAsync(RoadmapItemSource source, string externalId, CancellationToken ct);

    /// <summary>Inserts a new item, or replaces the mutable fields of an existing one with the same Id.</summary>
    Task UpsertAsync(RoadmapItem item, CancellationToken ct);

    Task AddChangeEventsAsync(IEnumerable<ChangeEvent> events, CancellationToken ct);

    /// <summary>Whether any ReleasePlanLine already references this item — drives the immediate-alert rule (design doc §6 step 6).</summary>
    Task<bool> IsReferencedByAnyReleasePlanAsync(Guid roadmapItemId, CancellationToken ct);
}
