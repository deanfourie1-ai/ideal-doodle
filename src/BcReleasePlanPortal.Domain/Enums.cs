namespace BcReleasePlanPortal.Domain;

/// <summary>Where a RoadmapItem was ingested from. See design doc §5.1.</summary>
public enum RoadmapItemSource
{
    Roadmap,
    LearnWhatsNew,
    LearnDeprecation,
    Isv,
    Manual,
}

/// <summary>
/// The kind of change a RoadmapItem represents. This is a derived field: rules run first,
/// AI may propose a value, but a human confirms during triage (design doc §5.1).
/// </summary>
public enum RoadmapChangeType
{
    Unknown,
    NewCapability,
    Enhancement,
    BehaviourChange,
    BreakingChange,
    Deprecation,
    Retirement,
}

public enum RoadmapItemStatus
{
    Unknown,
    Planned,
    Preview,
    Ga,
    Delayed,
    Cancelled,
}

public enum RoadmapEnabledBy
{
    Unknown,
    AdminsMakers,
    UsersAutomatically,
    FeatureManagement,
}

public enum EffortBand
{
    None,
    S,
    M,
    L,
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
}

/// <summary>CustomerItem.decision — kept in English internally; the curation UI may localize display (design doc §5.5).</summary>
public enum CustomerItemDecision
{
    Undecided,
    Adopt,
    TestFirst,
    Ignore,
    Blocked,
}

public enum CustomerItemRelevance
{
    Unscreened,
    Relevant,
    Watch,
    NotRelevant,
}

public enum ReleasePlanStatus
{
    Draft,
    Published,
    Superseded,
}
