namespace BcReleasePlanPortal.Domain;

/// <summary>
/// Written once against a RoadmapItem, reused across every matched customer. Design doc §5.3.
/// Not exercised by the ingest pipeline yet — schema only, for the curation UI phase.
/// </summary>
public class ImpactNote
{
    public Guid Id { get; set; }

    public Guid RoadmapItemId { get; set; }

    public RoadmapItem? RoadmapItem { get; set; }

    public string SummaryNl { get; set; } = string.Empty;

    public string WhyItMattersNl { get; set; } = string.Empty;

    public string ActionRequiredNl { get; set; } = string.Empty;

    public EffortBand EffortBand { get; set; } = EffortBand.None;

    public RiskLevel Risk { get; set; } = RiskLevel.Low;

    public string? Author { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
