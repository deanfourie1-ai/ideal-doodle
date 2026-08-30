using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Ingest.Diffing;

/// <summary>
/// Field-by-field diff between the previously stored state of a RoadmapItem and a freshly
/// normalized one, producing ChangeEvent rows. Design doc §5.2 / §6 step 5: this is what
/// catches a GA date sliding from October to April with nobody announcing it. Only called once
/// <see cref="PayloadHasher"/> has already shown the two payloads differ — this is the "which
/// field, and what changed" detail behind that yes/no signal.
/// </summary>
public static class ChangeEventDetector
{
    public static List<ChangeEvent> Detect(RoadmapItem previous, RoadmapItem current, DateTimeOffset detectedAt)
    {
        var events = new List<ChangeEvent>();

        void Compare<T>(string field, T oldValue, T newValue)
        {
            if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            {
                events.Add(new ChangeEvent
                {
                    Field = field,
                    OldValue = Format(oldValue),
                    NewValue = Format(newValue),
                    DetectedAt = detectedAt,
                });
            }
        }

        Compare(nameof(RoadmapItem.Title), previous.Title, current.Title);
        Compare(nameof(RoadmapItem.Status), previous.Status, current.Status);
        Compare(nameof(RoadmapItem.ChangeType), previous.ChangeType, current.ChangeType);
        Compare(nameof(RoadmapItem.TargetVersion), previous.TargetVersion, current.TargetVersion);
        Compare(nameof(RoadmapItem.PreviewDate), previous.PreviewDate, current.PreviewDate);
        Compare(nameof(RoadmapItem.GaDate), previous.GaDate, current.GaDate);
        Compare(nameof(RoadmapItem.EnabledBy), previous.EnabledBy, current.EnabledBy);

        var oldModules = string.Join(", ", previous.Modules.OrderBy(m => m, StringComparer.Ordinal));
        var newModules = string.Join(", ", current.Modules.OrderBy(m => m, StringComparer.Ordinal));
        Compare(nameof(RoadmapItem.Modules), oldModules, newModules);

        var oldObjects = string.Join(", ", previous.ObjectsTouched.OrderBy(o => o, StringComparer.Ordinal));
        var newObjects = string.Join(", ", current.ObjectsTouched.OrderBy(o => o, StringComparer.Ordinal));
        Compare(nameof(RoadmapItem.ObjectsTouched), oldObjects, newObjects);

        if (previous.DescriptionRaw != current.DescriptionRaw)
        {
            events.Add(new ChangeEvent
            {
                Field = nameof(RoadmapItem.DescriptionRaw),
                OldValue = Truncate(previous.DescriptionRaw),
                NewValue = Truncate(current.DescriptionRaw),
                DetectedAt = detectedAt,
            });
        }

        return events;
    }

    /// <summary>
    /// Whether this batch of ChangeEvents should trigger the immediate Teams alert (design doc
    /// §6 step 6) rather than waiting for the monthly triage: a new breaking change/deprecation
    /// classification, or a GA date move on an item already published in a customer plan.
    /// <paramref name="alreadyPublished"/> is supplied by the caller (the Data layer knows
    /// whether any ReleasePlanLine references this item) since Ingest has no knowledge of plans.
    /// </summary>
    public static bool RequiresImmediateAlert(RoadmapItem current, IReadOnlyList<ChangeEvent> events, bool alreadyPublished)
    {
        var isUrgentType = current.ChangeType is RoadmapChangeType.BreakingChange or RoadmapChangeType.Deprecation or RoadmapChangeType.Retirement;
        var changeTypeEvent = events.Any(e => e.Field == nameof(RoadmapItem.ChangeType));
        var gaMoved = events.Any(e => e.Field == nameof(RoadmapItem.GaDate));

        return (isUrgentType && changeTypeEvent) || (alreadyPublished && gaMoved);
    }

    private static string? Format<T>(T value) => value switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        _ => value.ToString(),
    };

    private static string Truncate(string value, int max = 200) => value.Length <= max ? value : value[..max] + "…";
}
