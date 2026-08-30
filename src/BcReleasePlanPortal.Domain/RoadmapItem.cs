namespace BcReleasePlanPortal.Domain;

/// <summary>
/// The canonical, product-side record: one row per Microsoft feature. Design doc §5.1.
///
/// <see cref="Product"/> is a free-text internal product code (e.g. "bc"), not a fixed enum —
/// which product codes exist and which Microsoft product tags map to them is ingest
/// configuration (<c>RoadmapIngestOptions.ProductFilters</c>), not something baked into the schema.
/// This keeps the door open to ingesting any Microsoft product family the business sells,
/// not just Business Central.
/// </summary>
public class RoadmapItem
{
    public Guid Id { get; set; }

    public RoadmapItemSource Source { get; set; }

    /// <summary>Microsoft's own identifier for this item, unique per source.</summary>
    public required string ExternalId { get; set; }

    public required string Product { get; set; }

    public required string Title { get; set; }

    public string DescriptionRaw { get; set; } = string.Empty;

    public string? Url { get; set; }

    public List<string> Modules { get; set; } = [];

    public RoadmapChangeType ChangeType { get; set; } = RoadmapChangeType.Unknown;

    /// <summary>
    /// True when the classifier could not confidently derive <see cref="ChangeType"/> or
    /// <see cref="Modules"/> and a human must confirm during triage (design doc §5.1, §8 Screen 1).
    /// </summary>
    public bool NeedsConfirmation { get; set; }

    public string? TargetVersion { get; set; }

    public DateOnly? PreviewDate { get; set; }

    public DateOnly? GaDate { get; set; }

    public RoadmapItemStatus Status { get; set; } = RoadmapItemStatus.Unknown;

    public RoadmapEnabledBy EnabledBy { get; set; } = RoadmapEnabledBy.Unknown;

    /// <summary>Table/page/codeunit names touched, from deprecation docs. Empty until a Learn source supplies it.</summary>
    public List<string> ObjectsTouched { get; set; } = [];

    /// <summary>Hash of the normalized payload, used for delta detection on re-ingest.</summary>
    public required string PayloadHash { get; set; }

    /// <summary>
    /// The source system's own "modified" timestamp (e.g. the MCP item's <c>modified</c>
    /// field). Not part of <see cref="PayloadHash"/> — it's a cheap pre-filter so a daily
    /// re-fetch only re-hydrates and re-normalizes items Microsoft actually touched, instead of
    /// paying the hydrate call for every item on every run (design doc §6 steps 1-2).
    /// </summary>
    public DateTimeOffset? SourceModifiedAt { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<ChangeEvent> ChangeEvents { get; set; } = [];
}
