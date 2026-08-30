namespace BcReleasePlanPortal.Domain;

/// <summary>
/// The join between a RoadmapItem and a Customer, and where the reuse payoff lives: ten
/// customers affected by the same deprecation share one ImpactNote and get ten short
/// overrides. Design doc §5.5. Not exercised by the ingest pipeline yet — schema only,
/// for the match engine and curation UI phases.
/// </summary>
public class CustomerItem
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public Guid RoadmapItemId { get; set; }

    public RoadmapItem? RoadmapItem { get; set; }

    public int MatchScore { get; set; }

    public List<string> MatchReasons { get; set; } = [];

    public CustomerItemRelevance Relevance { get; set; } = CustomerItemRelevance.Unscreened;

    public CustomerItemDecision Decision { get; set; } = CustomerItemDecision.Undecided;

    /// <summary>Customer-specific addendum to the shared ImpactNote — usually empty.</summary>
    public string OverrideNl { get; set; } = string.Empty;

    public string? Owner { get; set; }

    public string? TargetWindow { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? DecidedBy { get; set; }
}
