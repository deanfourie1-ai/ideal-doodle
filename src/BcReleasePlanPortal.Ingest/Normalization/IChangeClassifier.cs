using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Ingest.Normalization;

public readonly record struct ChangeClassification(RoadmapChangeType ChangeType, bool Confident);

/// <summary>
/// Derives <see cref="RoadmapChangeType"/> from an item's title/description. Design doc §5.1:
/// "Derive it with rules first ..., then let AI propose, then a human confirms during triage."
/// This interface is the seam for that AI-proposal step later — <see cref="RuleBasedChangeClassifier"/>
/// is the "rules first" half, deliberately deterministic and dependency-free (doc §6, core
/// principle #3: "Deterministic ingest, AI enrichment" — AI is never the only source of truth
/// and never runs in the ingest path itself).
/// </summary>
public interface IChangeClassifier
{
    ChangeClassification Classify(string title, string description);
}
