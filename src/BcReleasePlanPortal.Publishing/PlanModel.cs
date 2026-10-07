using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Publishing;

/// <summary>The document's sections (design doc §9.1), in the order they appear.</summary>
public enum PlanSection
{
    Mandatory,
    Recommended,
    ForInformation,
}

public sealed record PlanLine(
    Guid RoadmapItemId,
    string Title,
    PlanSection Section,
    RoadmapChangeType ChangeType,
    string? TargetVersion,
    DateOnly? GaDate,
    EffortBand EffortBand,
    RiskLevel Risk,
    CustomerItemDecision Decision,
    string Summary,
    string WhyItMatters,
    string Action,
    string CustomerNote,
    bool HasReviewedNote);

public sealed record PlanChanges(
    string PreviousVersion,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<(string Title, CustomerItemDecision From, CustomerItemDecision To)> DecisionChanged);

public sealed record PlanDocument(
    string CustomerName,
    bool IsSample,
    bool IsPreview,
    string Version,
    string PeriodLabel,
    DateOnly Date,
    IReadOnlyList<PlanLine> Lines,
    PlanChanges? Changes);

/// <summary>
/// Which items go into a customer's plan, and where. The rules, in order:
/// <list type="number">
/// <item>Ignored items are left out — the customer decided they don't apply.</item>
/// <item>Deprecations, retirements and breaking changes are <b>Mandatory</b>, decided or not: not
/// deciding doesn't make a removal go away.</item>
/// <item>Adopt and Test first are <b>Recommended</b> new functionality.</item>
/// <item>Blocked items are <b>For information</b> — known, but waiting on something.</item>
/// <item>Anything still Undecided (and not urgent) is left out: it hasn't been discussed yet.</item>
/// </list>
/// </summary>
public static class PlanRules
{
    public static PlanSection? SectionFor(RoadmapChangeType changeType, CustomerItemDecision decision)
    {
        if (decision == CustomerItemDecision.Ignore)
        {
            return null;
        }

        if (changeType is RoadmapChangeType.Deprecation or RoadmapChangeType.Retirement or RoadmapChangeType.BreakingChange)
        {
            return PlanSection.Mandatory;
        }

        return decision switch
        {
            CustomerItemDecision.Adopt or CustomerItemDecision.TestFirst => PlanSection.Recommended,
            CustomerItemDecision.Blocked => PlanSection.ForInformation,
            _ => null,
        };
    }

    /// <summary>Plan order: by section, then by when it lands, then by title.</summary>
    public static IReadOnlyList<PlanLine> Order(IEnumerable<PlanLine> lines) =>
        lines.OrderBy(l => l.Section)
            .ThenBy(l => l.GaDate ?? DateOnly.MaxValue)
            .ThenBy(l => l.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static PlanChanges Compare(string previousVersion, IReadOnlyList<PlanLine> previous, IReadOnlyList<PlanLine> current)
    {
        var before = previous.ToDictionary(l => l.RoadmapItemId);
        var after = current.ToDictionary(l => l.RoadmapItemId);
        return new PlanChanges(
            previousVersion,
            current.Where(l => !before.ContainsKey(l.RoadmapItemId)).Select(l => l.Title).ToList(),
            previous.Where(l => !after.ContainsKey(l.RoadmapItemId)).Select(l => l.Title).ToList(),
            current.Where(l => before.TryGetValue(l.RoadmapItemId, out var old) && old.Decision != l.Decision)
                .Select(l => (l.Title, before[l.RoadmapItemId].Decision, l.Decision))
                .ToList());
    }

    /// <summary>Next version number: 1.0 first, then 1.1, 1.2 …</summary>
    public static string NextVersion(string? latest)
    {
        if (latest is null)
        {
            return "1.0";
        }

        var parts = latest.Split('.');
        return $"{parts[0]}.{int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) + 1}";
    }

    /// <summary>Default period: this month plus the next five — one release wave.</summary>
    public static string DefaultPeriod(DateOnly today)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(5);
        var english = System.Globalization.CultureInfo.InvariantCulture;
        return $"{start.ToString("MMMM yyyy", english)} – {end.ToString("MMMM yyyy", english)}";
    }
}
