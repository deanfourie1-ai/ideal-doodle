using System.Text.RegularExpressions;
using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Diffing;
using BcReleasePlanPortal.Ingest.Normalization;

namespace BcReleasePlanPortal.Ingest.Learn;

/// <summary>
/// Maps a deprecated-features section to a <see cref="RoadmapItem"/> with
/// <see cref="RoadmapItemSource.LearnDeprecation"/>. Unlike roadmap posts, the change type here is
/// not a keyword guess — the page states it — so it's reported confident; only the module
/// classification can still send an item to triage.
/// </summary>
public sealed partial class LearnDeprecationNormalizer(IModuleClassifier moduleClassifier)
{
    public RoadmapItem Normalize(LearnDeprecation source, string internalProduct, int currentMajorVersion, DateTimeOffset now)
    {
        var modules = moduleClassifier.Classify(source.Title, source.Description, []);
        var major = LearnTargetVersions.MajorOf(source.Version);

        var item = new RoadmapItem
        {
            Id = Guid.NewGuid(),
            Source = RoadmapItemSource.LearnDeprecation,
            ExternalId = ExternalIdFor(source),
            Product = internalProduct,
            Title = source.Title,
            DescriptionRaw = source.Description,
            Url = source.Url,
            Modules = [.. modules.Modules],
            ChangeType = ClassifyChange(source),
            NeedsConfirmation = !modules.Confident,
            TargetVersion = source.Version,
            // Release waves start in April (wave 1) and October (wave 2): the date the change takes effect.
            GaDate = new DateOnly(source.WaveYear, source.WaveNumber == 1 ? 4 : 10, 1),
            Status = major <= currentMajorVersion ? RoadmapItemStatus.Ga : RoadmapItemStatus.Planned,
            EnabledBy = RoadmapEnabledBy.Unknown,
            ObjectsTouched = [],
            PayloadHash = string.Empty,
            FirstSeenAt = now,
            LastSeenAt = now,
        };

        item.PayloadHash = PayloadHasher.Compute(item);
        return item;
    }

    /// <summary>
    /// Version plus a slug of the title. Not the page's own anchor: Learn numbers duplicate anchors by
    /// position ("excel-reports-…-1"), so a new section added higher up would renumber the rest.
    /// The version disambiguates the same feature's warning and its later removal.
    /// </summary>
    public static string ExternalIdFor(LearnDeprecation source) =>
        $"{source.Version}/{NonSlugCharacters().Replace(source.Title.ToLowerInvariant(), "-").Trim('-')}";

    /// <summary>
    /// The page's own words, in order of strength: a "(removal)" title or a "Removed" state means it's
    /// gone in that version; "(warning)" means obsolete-pending; a move is a behaviour change (the
    /// feature still exists, somewhere else); otherwise it's being replaced — a deprecation.
    /// </summary>
    public static RoadmapChangeType ClassifyChange(LearnDeprecation source)
    {
        if (source.Title.Contains("(removal)", StringComparison.OrdinalIgnoreCase))
        {
            return RoadmapChangeType.Retirement;
        }

        if (source.Title.Contains("(warning)", StringComparison.OrdinalIgnoreCase))
        {
            return RoadmapChangeType.Deprecation;
        }

        if (source.State.Contains("Removed", StringComparison.OrdinalIgnoreCase))
        {
            return RoadmapChangeType.Retirement;
        }

        return source.State.Equals("Moved", StringComparison.OrdinalIgnoreCase)
            ? RoadmapChangeType.BehaviourChange
            : RoadmapChangeType.Deprecation;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
