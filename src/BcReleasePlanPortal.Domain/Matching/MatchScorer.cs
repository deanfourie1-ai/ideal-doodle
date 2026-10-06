using System.Text.RegularExpressions;

namespace BcReleasePlanPortal.Domain.Matching;

public sealed record MatchResult(int Score, IReadOnlyList<string> Reasons)
{
    public bool IsCandidate => Score >= MatchScorer.CandidateThreshold;
}

/// <summary>
/// Scores one <see cref="RoadmapItem"/> against one <see cref="Customer"/> (design doc §7), with a
/// human-readable reason for every point, so a consultant can see *why* an item is on a customer's
/// list and disagree with it.
/// <para>
/// Weights: the doc fixes two — module overlap +30, objects touched ∩ extended objects +40. It names
/// the SOAP, ISV, version and enabled-by rules and the out-of-scope penalty without weights in the
/// material available on 2026-10-06, so those below are provisional, set so that any single named
/// dependency (SOAP, an app, a standard) is enough on its own to make an item a candidate, and
/// urgency amplifies relevance but never creates it. Tune against real profiles.
/// </para>
/// </summary>
public static partial class MatchScorer
{
    public const int CandidateThreshold = 30;

    public const int ModuleOverlap = 30;
    public const int UnconfirmedModuleOverlap = 15;
    public const int ObjectOverlap = 40;
    public const int SoapDependency = 40;
    public const int AppDependency = 40;
    public const int NamedDependency = 40;
    public const int CopilotInUse = 20;
    public const int UrgentAndRelevant = 20;
    public const int EnabledAutomatically = 10;
    public const int OutsideScope = -20;

    public static MatchResult Score(RoadmapItem item, Customer customer)
    {
        var score = 0;
        var reasons = new List<string>();
        void Add(int points, string reason)
        {
            score += points;
            reasons.Add($"{(points > 0 ? "+" : "")}{points} {reason}");
        }

        var title = item.Title;
        var text = $"{item.Title}\n{item.DescriptionRaw}";

        // Until a person has triaged an item, its module tags are the keyword classifier's guess; on
        // the first real data most "Reporting" tags came from words like "report" in passing. A guess
        // counts half — on its own it doesn't make a candidate — so triage is what turns module-only
        // matches on. (Not NeedsConfirmation: a Learn deprecation is unflagged as soon as the
        // classifier guesses any module at all.)
        var modules = item.Modules.Intersect(customer.ModulesInUse).ToList();
        if (modules.Count > 0)
        {
            if (item.TriagedAt is null)
            {
                Add(UnconfirmedModuleOverlap, $"uses {string.Join(", ", modules)} (module tags not yet confirmed in triage)");
            }
            else
            {
                Add(ModuleOverlap, $"uses {string.Join(", ", modules)}");
            }
        }

        var objects = item.ObjectsTouched
            .Where(o => customer.CustomExtensions.Any(e => e.ExtendsObjects.Contains(o, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        if (objects.Count > 0)
        {
            Add(ObjectOverlap, $"custom extensions touch {string.Join(", ", objects)}");
        }

        if (customer.Integrations.Soap && SoapPattern().IsMatch(text))
        {
            var endpoints = customer.Integrations.SoapEndpoints.Count > 0 ? $" ({string.Join(", ", customer.Integrations.SoapEndpoints)})" : "";
            Add(SoapDependency, $"publishes SOAP endpoints{endpoints}");
        }

        // Apps and named dependencies are matched on the title only: descriptions mention words like
        // "document" or "output" too loosely, and the title is where Microsoft names what changes.
        foreach (var app in customer.AppSourceApps.Where(a => NameAppearsIn(a.Name, title)))
        {
            Add(AppDependency, $"has {app.Name} installed");
        }

        foreach (var dependency in customer.Integrations.Other.Where(d => NameAppearsIn(d, title)))
        {
            Add(NamedDependency, $"depends on {dependency}");
        }

        if (customer.Flags.UsesCopilot && CopilotPattern().IsMatch(title))
        {
            Add(CopilotInUse, "uses Copilot and agents");
        }

        if (item.EnabledBy == RoadmapEnabledBy.UsersAutomatically && score > 0)
        {
            Add(EnabledAutomatically, "switches on automatically for users");
        }

        if (score > 0 && item.ChangeType is RoadmapChangeType.Deprecation or RoadmapChangeType.Retirement or RoadmapChangeType.BreakingChange)
        {
            var when = item.TargetVersion is null ? "" : $" in {item.TargetVersion}{VersionContext(item, customer)}";
            Add(UrgentAndRelevant, $"{item.ChangeType.ToString().ToLowerInvariant()}{when} — action needed");
        }

        if (modules.Count == 0 && item.Modules.Count > 0
            && item.ChangeType is RoadmapChangeType.NewCapability or RoadmapChangeType.Enhancement)
        {
            Add(OutsideScope, $"new functionality in {string.Join(", ", item.Modules)}, which they don't use");
        }

        return new MatchResult(Math.Max(score, 0), reasons);
    }

    private static string VersionContext(RoadmapItem item, Customer customer)
    {
        var production = customer.Environments.FirstOrDefault(e => e.Name.Equals("Production", StringComparison.OrdinalIgnoreCase))
            ?? customer.Environments.FirstOrDefault();
        return production is null || string.IsNullOrEmpty(production.Version) ? "" : $" (production is on {production.Version})";
    }

    /// <summary>
    /// A name "appears" when all of its distinctive words do, as whole words, generic words aside.
    /// "AMC Banking 365 Fundamentals" matches "AMC Fundamentals app (removal)"; "Shopify Connector"
    /// matches any title naming Shopify. All, not most: with two-of-three, "Finance reports API"
    /// matched "Trace G/L account usage in finance reports" on the first real run.
    /// </summary>
    public static bool NameAppearsIn(string name, string title)
    {
        var words = WordPattern().Matches(name)
            .Select(m => m.Value)
            .Where(w => !GenericWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (words.Count == 0)
        {
            return false;
        }

        var found = words.Count(w => Regex.IsMatch(title, $@"(?<![\w.]){Regex.Escape(w)}(?![\w])", RegexOptions.IgnoreCase));
        return found == words.Count;
    }

    private static readonly HashSet<string> GenericWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "365", "app", "apps", "connector", "for", "the", "and", "with", "via", "over", "of", "to",
        "business", "central", "dynamics", "microsoft", "beta", "banking", "import", "statement",
    };

    [GeneratedRegex(@"[\w.]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\bSOAP\b", RegexOptions.IgnoreCase)]
    private static partial Regex SoapPattern();

    [GeneratedRegex(@"\b(copilot|agents?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CopilotPattern();
}
