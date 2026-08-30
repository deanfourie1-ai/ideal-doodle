using System.Text.RegularExpressions;
using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Ingest.Normalization;

/// <summary>
/// Keyword rules per design doc §5.1: "title/body contains deprecat*, removed, no longer,
/// breaking". The urgent categories (deprecation/retirement/breaking change) are the ones a
/// keyword match can call with real confidence — those words are rarely used loosely in a
/// roadmap post. Everything else (new capability vs. enhancement vs. behaviour change) is a
/// soft guess from weaker signal words and always comes back <c>Confident: false</c>, so the
/// normalizer flags it <c>NeedsConfirmation</c> for human triage (doc §8 Screen 1) rather than
/// silently asserting a classification the rules can't actually support.
/// </summary>
public sealed partial class RuleBasedChangeClassifier : IChangeClassifier
{
    public ChangeClassification Classify(string title, string description)
    {
        var text = $"{title}\n{description}";

        if (RetirementPattern().IsMatch(text))
        {
            return new ChangeClassification(RoadmapChangeType.Retirement, Confident: true);
        }

        if (DeprecationPattern().IsMatch(text))
        {
            return new ChangeClassification(RoadmapChangeType.Deprecation, Confident: true);
        }

        if (BreakingChangePattern().IsMatch(text))
        {
            return new ChangeClassification(RoadmapChangeType.BreakingChange, Confident: true);
        }

        if (BehaviourChangePattern().IsMatch(text))
        {
            return new ChangeClassification(RoadmapChangeType.BehaviourChange, Confident: false);
        }

        if (NewCapabilityPattern().IsMatch(title))
        {
            return new ChangeClassification(RoadmapChangeType.NewCapability, Confident: false);
        }

        return new ChangeClassification(RoadmapChangeType.Enhancement, Confident: false);
    }

    [GeneratedRegex(@"\bretir(e|ed|ement|ing)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RetirementPattern();

    [GeneratedRegex(@"\bdeprecat(e|ed|ion|ing)\b|\bno longer (be )?(available|supported|works?)\b|\bremoved?\b", RegexOptions.IgnoreCase)]
    private static partial Regex DeprecationPattern();

    [GeneratedRegex(@"\bbreaking change\b|\bwill break\b", RegexOptions.IgnoreCase)]
    private static partial Regex BreakingChangePattern();

    [GeneratedRegex(@"\bbehaviou?r (is )?chang(e|ed|ing)\b|\bchanges? (the )?(default )?behaviou?r\b", RegexOptions.IgnoreCase)]
    private static partial Regex BehaviourChangePattern();

    [GeneratedRegex(@"^\s*(new:|introduc|now available)", RegexOptions.IgnoreCase)]
    private static partial Regex NewCapabilityPattern();
}
