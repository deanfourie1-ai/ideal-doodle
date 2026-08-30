namespace BcReleasePlanPortal.Domain;

/// <summary>
/// A frozen snapshot. Publishing copies the current curated text into ReleasePlanLine so the
/// document never changes retroactively. Design doc §5.6. Not exercised by the ingest
/// pipeline yet — schema only, for the publish phase.
/// </summary>
public class ReleasePlan
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>e.g. "1.0", "1.1".</summary>
    public required string Version { get; set; }

    /// <summary>e.g. "Oktober 2026 – Maart 2027".</summary>
    public required string PeriodLabel { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public ReleasePlanStatus Status { get; set; } = ReleasePlanStatus.Draft;

    public string? DocumentPath { get; set; }

    public List<ReleasePlanLine> Lines { get; set; } = [];
}

public class ReleasePlanLine
{
    public Guid Id { get; set; }

    public Guid ReleasePlanId { get; set; }

    public ReleasePlan? ReleasePlan { get; set; }

    public Guid RoadmapItemId { get; set; }

    public required string TitleNl { get; set; }

    public string SummaryNl { get; set; } = string.Empty;

    public string ActionNl { get; set; } = string.Empty;

    public RoadmapChangeType ChangeType { get; set; }

    public DateOnly? GaDate { get; set; }

    public EffortBand EffortBand { get; set; }

    public CustomerItemDecision Decision { get; set; }

    public string? Owner { get; set; }

    public int SortOrder { get; set; }
}
