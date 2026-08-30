namespace BcReleasePlanPortal.Domain;

/// <summary>
/// Written once against a RoadmapItem, reused across every matched customer. Design doc §5.3.
///
/// Copy is authored in English. The design doc (§13 open decision 2) left the working language
/// open and leaned toward "English internally, Dutch only at the publish boundary"; that decision
/// has since been resolved in favour of English throughout — schema, curation UI, and the
/// published document alike. Translation, if it is ever wanted, becomes a layer over these fields
/// rather than a property of them, so nothing here is language-suffixed.
///
/// Not exercised by the ingest pipeline yet — schema only, for the curation UI phase.
/// </summary>
public class ImpactNote
{
    public Guid Id { get; set; }

    public Guid RoadmapItemId { get; set; }

    public RoadmapItem? RoadmapItem { get; set; }

    /// <summary>2–4 sentences, customer-readable.</summary>
    public string Summary { get; set; } = string.Empty;

    public string WhyItMatters { get; set; } = string.Empty;

    /// <summary>What someone must actually do.</summary>
    public string ActionRequired { get; set; } = string.Empty;

    public EffortBand EffortBand { get; set; } = EffortBand.None;

    public RiskLevel Risk { get; set; } = RiskLevel.Low;

    public string? Author { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
