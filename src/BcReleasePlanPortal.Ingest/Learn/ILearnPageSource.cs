namespace BcReleasePlanPortal.Ingest.Learn;

public sealed record LearnPageItem(string Title, string Url, IReadOnlyList<string> ObjectsTouched);

public sealed record LearnPageResult(bool Available, IReadOnlyList<LearnPageItem> Items, string? UnavailableReason);

/// <summary>
/// Design doc §6 step 3: scrape "What's new and changed in update N" and the deprecated
/// features pages on Microsoft Learn — the only source for <see cref="Domain.RoadmapItem.ObjectsTouched"/>
/// and <see cref="Domain.RoadmapItem.TargetVersion"/>. Doc §13 risk mitigation: "Scrapers
/// isolated behind an interface, fail loudly, degrade to link-only." See
/// <see cref="UnavailableLearnPageSource"/> for why this can't be a real scraper yet.
/// </summary>
public interface ILearnPageSource
{
    Task<LearnPageResult> FetchWhatsNewAsync(string version, CancellationToken ct);

    Task<LearnPageResult> FetchDeprecatedFeaturesAsync(CancellationToken ct);
}
