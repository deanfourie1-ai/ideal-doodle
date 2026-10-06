namespace BcReleasePlanPortal.Ingest.Configuration;

/// <summary>
/// Bound from the "RoadmapIngest" configuration section.
///
/// <see cref="ProductFilters"/> maps our own internal product codes onto Microsoft's roadmap
/// "products" tag names. This is deliberately data, not code: the live MCP/REST surface at
/// <see cref="McpEndpoint"/> was verified (2026-08-30) to carry only Microsoft 365 and Azure
/// products — Dynamics 365 Business Central is not present on it yet, even though the design
/// doc's premise is that BC roadmap content lands there from September 2026. Rather than block
/// on that, the default configuration below filters for both "bc" (correct, currently returns
/// zero items) and a second product that already has live data today, so the whole pipeline —
/// fetch, hydrate, normalize, hash/diff, ChangeEvent — is provably working end to end right now.
/// Add more entries here as the business ingests roadmap data for other Microsoft platforms.
/// </summary>
public sealed class RoadmapIngestOptions
{
    public const string SectionName = "RoadmapIngest";

    public string McpEndpoint { get; set; } = "https://www.microsoft.com/releasecommunications/mcp";

    public List<ProductFilter> ProductFilters { get; set; } = [];

    /// <summary>Business Central dev-itpro root on Learn; must end in "/" so relative page paths resolve under it.</summary>
    public string LearnBaseUrl { get; set; } = "https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/";

    /// <summary>
    /// How many of the most recent major updates' "What's new" pages to read for target versions.
    /// Two covers the current wave plus the previous one, which is where anything still in flight lives.
    /// </summary>
    public int LearnMajorVersionsToRead { get; set; } = 2;

    /// <summary>Internal product the Learn deprecated-features items are filed under (Learn's pages are BC-only).</summary>
    public string LearnInternalProduct { get; set; } = "bc";

    /// <summary>Incoming webhook URL for the Teams alert channel (design doc §6 step 6). Null = log-only.</summary>
    public string? TeamsWebhookUrl { get; set; }

    /// <summary>Local time of day the daily ingest job runs, "HH:mm", interpreted in <see cref="TimeZoneId"/>.</summary>
    public string DailyRunTime { get; set; } = "06:00";

    public string TimeZoneId { get; set; } = "Europe/Amsterdam";
}

/// <summary>One entry in <see cref="RoadmapIngestOptions.ProductFilters"/>.</summary>
public sealed class ProductFilter
{
    /// <summary>Our own product code, stored on <see cref="Domain.RoadmapItem.Product"/> — e.g. "bc".</summary>
    public required string InternalProduct { get; set; }

    /// <summary>Microsoft "products" tag names that map to <see cref="InternalProduct"/>.</summary>
    public required List<string> MicrosoftProductTags { get; set; }
}
