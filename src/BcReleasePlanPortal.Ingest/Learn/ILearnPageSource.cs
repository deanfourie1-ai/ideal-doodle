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

/// <summary>One feature section of the deprecated-features page.</summary>
/// <param name="Version">BC major version the change lands in, e.g. "30.0" — from the wave heading.</param>
/// <param name="State">"Moved", "Removed" or "Replaced", as printed; several values joined when a section has several rows.</param>
public sealed record LearnDeprecation(
    string Title,
    string Url,
    int WaveYear,
    int WaveNumber,
    string Version,
    string State,
    string Description);

public sealed record LearnDeprecationResult(bool Available, IReadOnlyList<LearnDeprecation> Items, string? UnavailableReason);

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

    Task<LearnDeprecationResult> FetchDeprecatedFeaturesAsync(CancellationToken ct);
}
