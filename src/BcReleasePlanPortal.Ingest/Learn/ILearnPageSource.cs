namespace BcReleasePlanPortal.Ingest.Learn;

/// <param name="RoadmapId">
/// The roadmap item the Learn row points at, when the page links one. "What's new" pages for a
/// major update (x.0) carry a Roadmap ID column — that's the exact join to <c>RoadmapItem.ExternalId</c>.
/// </param>
/// <param name="Availability">As printed on the page, e.g. "General availability" or "Public preview".</param>
public sealed record LearnPageItem(
    string Title,
    string Url,
    string? RoadmapId,
    string? ProductArea,
    string? Availability,
    IReadOnlyList<string> ObjectsTouched);

public sealed record LearnPageResult(bool Available, IReadOnlyList<LearnPageItem> Items, string? UnavailableReason);

/// <summary>
/// Design doc §6 step 3: scrape "What's new and changed in update N" and the deprecated
/// features pages on Microsoft Learn — the only source for <see cref="Domain.RoadmapItem.ObjectsTouched"/>
/// and <see cref="Domain.RoadmapItem.TargetVersion"/>. Doc §13 risk mitigation: "Scrapers
/// isolated behind an interface, fail loudly, degrade to link-only."
/// </summary>
public interface ILearnPageSource
{
    /// <summary>Every update version Learn publishes a "What's new" page for (e.g. "28.0", "28.1", "29.0"), oldest first.</summary>
    Task<IReadOnlyList<string>> ListWhatsNewVersionsAsync(CancellationToken ct);

    Task<LearnPageResult> FetchWhatsNewAsync(string version, CancellationToken ct);

    Task<LearnPageResult> FetchDeprecatedFeaturesAsync(CancellationToken ct);
}
