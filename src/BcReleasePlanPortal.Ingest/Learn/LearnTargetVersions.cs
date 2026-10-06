using System.Globalization;

namespace BcReleasePlanPortal.Ingest.Learn;

/// <summary>
/// Pure helpers behind the Learn → <see cref="Domain.RoadmapItem.TargetVersion"/> enrichment, kept
/// separate from the ingest service so the rules are unit-testable without HTTP or a database.
/// </summary>
public static class LearnTargetVersions
{
    /// <summary>
    /// The last <paramref name="count"/> major updates (x.0) from a version list, oldest first. Only
    /// major-update pages carry a Roadmap ID column, so minor updates add nothing to the join.
    /// </summary>
    public static IReadOnlyList<string> MostRecentMajors(IReadOnlyList<string> versions, int count) =>
        versions
            .Where(v => v.EndsWith(".0", StringComparison.Ordinal))
            .OrderBy(MajorOf)
            .TakeLast(count)
            .ToList();

    public static int MajorOf(string version) => int.Parse(version.Split('.')[0], CultureInfo.InvariantCulture);

    /// <summary>
    /// Roadmap ID → the update an item becomes generally available in. Rows marked as a preview are
    /// skipped: update 29.0 lists Expense Agent features as "Public preview" whose GA is April 2027,
    /// and calling 29.0 their target version would mislead a customer plan (preview timing already
    /// lives in <see cref="Domain.RoadmapItem.PreviewDate"/>). Pages are applied oldest first, so a
    /// later update wins if an item is listed GA on two.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Build(IEnumerable<(string Version, LearnPageResult Page)> pages)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (version, page) in pages.Where(p => p.Page.Available))
        {
            foreach (var item in page.Items.Where(i => i.RoadmapId is not null && !IsPreview(i.Availability)))
            {
                map[item.RoadmapId!] = version;
            }
        }

        return map;
    }

    private static bool IsPreview(string? availability) =>
        availability?.Contains("preview", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Learn only ever sets or moves a version, never clears one: an item missing from the pages read
    /// (Learn unreachable, or listed on an older update than we read) keeps what it had.
    /// </summary>
    public static string? Resolve(IReadOnlyDictionary<string, string>? map, string roadmapId, string? knownVersion) =>
        map is not null && map.TryGetValue(roadmapId, out var version) ? version : knownVersion;
}
