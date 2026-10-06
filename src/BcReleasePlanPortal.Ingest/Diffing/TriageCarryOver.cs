using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Ingest.Diffing;

/// <summary>
/// Keeps a person's triage decision across re-ingests. Without this, the next time Microsoft
/// touches an item the classifiers would silently overwrite a confirmed or corrected
/// <see cref="RoadmapItem.ChangeType"/>/<see cref="RoadmapItem.Modules"/>.
/// <para>
/// One exception, deliberately: if the fresh classification is urgent (deprecation, retirement,
/// breaking change) and differs from the human one, the classifier wins and the item goes back
/// to the triage queue. Those types are only ever produced from explicit wording ("deprecated",
/// "removed", the Learn deprecations page), so this is Microsoft changing the story — a human
/// "Enhancement" from last month must never hide a removal announced today.
/// </para>
/// </summary>
public static class TriageCarryOver
{
    /// <summary>Applies to <paramref name="current"/> in place and recomputes its hash. Returns true if the item was sent back to triage.</summary>
    public static bool Apply(RoadmapItem existing, RoadmapItem current)
    {
        if (existing.TriagedAt is null)
        {
            return false;
        }

        var sentBack = IsUrgent(current.ChangeType) && current.ChangeType != existing.ChangeType;
        if (sentBack)
        {
            current.Modules = [.. existing.Modules];
            current.NeedsConfirmation = true;
            current.TriagedAt = null;
        }
        else
        {
            current.ChangeType = existing.ChangeType;
            current.Modules = [.. existing.Modules];
            current.NeedsConfirmation = false;
            current.TriagedAt = existing.TriagedAt;
        }

        current.PayloadHash = PayloadHasher.Compute(current);
        return sentBack;
    }

    public static bool IsUrgent(RoadmapChangeType changeType) =>
        changeType is RoadmapChangeType.Deprecation or RoadmapChangeType.Retirement or RoadmapChangeType.BreakingChange;
}
