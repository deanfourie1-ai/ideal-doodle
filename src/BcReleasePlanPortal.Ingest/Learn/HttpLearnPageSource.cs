using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace BcReleasePlanPortal.Ingest.Learn;

/// <summary>
/// Reads Business Central's "What's new" pages on Microsoft Learn. Structure verified against the
/// live pages on 2026-10-06 (fixtures in the test project):
/// <list type="bullet">
/// <item>The dev-itpro <c>toc.json</c> links every <c>whatsnew/whatsnew-update-{major}-{minor}</c> page, so
/// versions are discovered rather than configured.</item>
/// <item>Major-update pages (x.0) have a "New and updated features" table with columns Product area /
/// Feature / Availability / Roadmap ID. Product area is only printed on the first row of each group.</item>
/// <item>Minor-update pages (x.1–x.5) have a features table too, but no Roadmap ID column — so they're
/// parsed, but yield no join keys.</item>
/// </list>
/// Anything that doesn't match that shape returns <c>Available: false</c> with a reason and logs a
/// warning, rather than guessing (design doc §13: fail loudly, degrade to link-only).
/// </summary>
public sealed partial class HttpLearnPageSource(HttpClient httpClient, ILogger<HttpLearnPageSource> logger) : ILearnPageSource
{
    public async Task<IReadOnlyList<string>> ListWhatsNewVersionsAsync(CancellationToken ct)
    {
        var toc = await httpClient.GetStringAsync("toc.json", ct);
        var versions = ParseTocVersions(toc);
        if (versions.Count == 0)
        {
            logger.LogWarning("Learn toc.json listed no whatsnew-update pages; its structure may have changed");
        }

        return versions;
    }

    public async Task<LearnPageResult> FetchWhatsNewAsync(string version, CancellationToken ct)
    {
        var path = WhatsNewPath(version);
        try
        {
            var html = await httpClient.GetStringAsync(path, ct);
            var result = ParseWhatsNew(html, new Uri(httpClient.BaseAddress!, path).ToString());
            if (!result.Available)
            {
                logger.LogWarning("Learn 'what's new' page for BC update {Version} not parsed: {Reason}", version, result.UnavailableReason);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Learn 'what's new' page for BC update {Version} could not be fetched", version);
            return new LearnPageResult(false, [], $"Fetch failed: {ex.Message}");
        }
    }

    public const string DeprecatedFeaturesPath = "upgrade/deprecated-features-w1";

    public async Task<LearnDeprecationResult> FetchDeprecatedFeaturesAsync(CancellationToken ct)
    {
        try
        {
            var html = await httpClient.GetStringAsync(DeprecatedFeaturesPath, ct);
            var result = ParseDeprecatedFeatures(html, new Uri(httpClient.BaseAddress!, DeprecatedFeaturesPath).ToString());
            if (!result.Available)
            {
                logger.LogWarning("Learn deprecated-features page not parsed: {Reason}", result.UnavailableReason);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Learn deprecated-features page could not be fetched");
            return new LearnDeprecationResult(false, [], $"Fetch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Deprecated-features page, as observed 2026-10-06: an h2 per release wave ("Changes in 2027
    /// release wave 1 (version 30.0)" — the version is not always printed), then per feature an h3
    /// followed by a table with columns "Moved, Removed, or Replaced?" / "Why?". Most tables have
    /// one row; a few have several, which are folded into one item.
    /// </summary>
    public static LearnDeprecationResult ParseDeprecatedFeatures(string html, string pageUrl)
    {
        var document = new HtmlParser().ParseDocument(html);
        var root = (IParentNode?)document.QuerySelector("main") ?? document;

        var items = new List<LearnDeprecation>();
        (int Year, int Number, string Version)? wave = null;
        (string Title, string? Anchor)? feature = null;

        foreach (var element in root.QuerySelectorAll("h2, h3, table"))
        {
            switch (element)
            {
                case IHtmlHeadingElement { LocalName: "h2" } h2:
                    var match = WaveHeadingPattern().Match(h2.TextContent);
                    wave = match.Success
                        ? (int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture),
                           int.Parse(match.Groups["wave"].Value, CultureInfo.InvariantCulture),
                           match.Groups["version"].Success ? match.Groups["version"].Value : VersionForWave(
                               int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture),
                               int.Parse(match.Groups["wave"].Value, CultureInfo.InvariantCulture)))
                        : null;
                    feature = null;
                    break;

                case IHtmlHeadingElement h3:
                    feature = (h3.TextContent.Trim(), h3.Id);
                    break;

                case IHtmlTableElement table when wave is not null && feature is not null
                    && HeaderCells(table).FirstOrDefault()?.StartsWith("Moved", StringComparison.OrdinalIgnoreCase) == true:
                    var rows = table.Bodies.SelectMany(b => b.Rows)
                        .Select(r => r.Cells.Select(c => c.TextContent.Trim()).ToList())
                        .Where(cells => cells.Count >= 2)
                        .ToList();
                    if (rows.Count == 0)
                    {
                        break;
                    }

                    items.Add(new LearnDeprecation(
                        Title: feature.Value.Title,
                        Url: feature.Value.Anchor is null ? pageUrl : $"{pageUrl}#{feature.Value.Anchor}",
                        WaveYear: wave.Value.Year,
                        WaveNumber: wave.Value.Number,
                        Version: wave.Value.Version,
                        State: string.Join(", ", rows.Select(r => r[0]).Distinct()),
                        Description: string.Join("\n\n", rows.Select(r => r[1]))));
                    feature = null;
                    break;
            }
        }

        return items.Count == 0
            ? new LearnDeprecationResult(false, [], "No release-wave sections with feature tables found.")
            : new LearnDeprecationResult(true, items, null);
    }

    /// <summary>
    /// BC's major version for a release wave: two per year, 2022 wave 2 = 21, 2026 wave 1 = 28,
    /// 2027 wave 1 = 30 (all three printed on the live page). Used when a wave heading omits it.
    /// </summary>
    public static string VersionForWave(int year, int wave) => $"{(2 * year) + wave - 4025}.0";

    public static string WhatsNewPath(string version) => $"whatsnew/whatsnew-update-{version.Replace('.', '-')}";

    public static IReadOnlyList<string> ParseTocVersions(string tocJson) =>
        TocVersionPattern().Matches(tocJson)
            .Select(m => (Major: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Minor: int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
            .Distinct()
            .OrderBy(v => v.Major).ThenBy(v => v.Minor)
            .Select(v => $"{v.Major}.{v.Minor}")
            .ToList();

    public static LearnPageResult ParseWhatsNew(string html, string pageUrl)
    {
        var document = new HtmlParser().ParseDocument(html);

        var table = document.QuerySelectorAll("table").OfType<IHtmlTableElement>()
            .FirstOrDefault(t => HeaderCells(t).Any(h => h.Equals("Feature", StringComparison.OrdinalIgnoreCase)));
        if (table is null)
        {
            return new LearnPageResult(false, [], "No table with a 'Feature' column found.");
        }

        var headers = HeaderCells(table);
        var featureColumn = headers.FindIndex(h => h.Equals("Feature", StringComparison.OrdinalIgnoreCase));
        var areaColumn = headers.FindIndex(h => h.Equals("Product area", StringComparison.OrdinalIgnoreCase));
        var availabilityColumn = headers.FindIndex(h => h.Contains("availability", StringComparison.OrdinalIgnoreCase));
        var roadmapColumn = headers.FindIndex(h => h.Equals("Roadmap ID", StringComparison.OrdinalIgnoreCase));

        var items = new List<LearnPageItem>();
        string? currentArea = null;
        foreach (var row in table.Bodies.SelectMany(b => b.Rows))
        {
            var cells = row.Cells.Select(c => c.TextContent.Trim()).ToList();
            if (cells.Count != headers.Count)
            {
                continue;
            }

            if (areaColumn >= 0 && cells[areaColumn].Length > 0)
            {
                currentArea = cells[areaColumn];
            }

            var roadmapId = roadmapColumn >= 0 && RoadmapIdPattern().IsMatch(cells[roadmapColumn]) ? cells[roadmapColumn] : null;
            items.Add(new LearnPageItem(
                Title: cells[featureColumn],
                Url: pageUrl,
                RoadmapId: roadmapId,
                ProductArea: currentArea,
                Availability: availabilityColumn >= 0 ? cells[availabilityColumn] : null,
                ObjectsTouched: []));
        }

        return items.Count == 0
            ? new LearnPageResult(false, [], "Feature table found but it had no rows.")
            : new LearnPageResult(true, items, null);
    }

    private static List<string> HeaderCells(IHtmlTableElement table) =>
        table.Head?.Rows.FirstOrDefault()?.Cells.Select(c => c.TextContent.Trim()).ToList() ?? [];

    [GeneratedRegex(@"whatsnew-update-(\d+)-(\d+)")]
    private static partial Regex TocVersionPattern();

    [GeneratedRegex(@"Changes in (?<year>\d{4}) release wave (?<wave>[12])(?:\s*\(version (?<version>\d+\.\d+)\))?", RegexOptions.IgnoreCase)]
    private static partial Regex WaveHeadingPattern();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex RoadmapIdPattern();
}
