namespace BcReleasePlanPortal.Domain;

/// <summary>
/// Emitted whenever a re-ingest produces a different PayloadHash for a RoadmapItem.
/// The highest-value table in the system: a GA date sliding from October to April changes a
/// customer's plan, and under the continuous-publish model nobody announces it. The daily
/// diff catches it. Design doc §5.2.
/// </summary>
public class ChangeEvent
{
    public Guid Id { get; set; }

    public Guid RoadmapItemId { get; set; }

    public RoadmapItem? RoadmapItem { get; set; }

    /// <summary>Name of the changed field, e.g. "GaDate", "Status", "Title".</summary>
    public required string Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTimeOffset DetectedAt { get; set; }

    /// <summary>Whether this event has been pushed to the Teams alert channel (design doc §6 step 6).</summary>
    public bool Notified { get; set; }
}
