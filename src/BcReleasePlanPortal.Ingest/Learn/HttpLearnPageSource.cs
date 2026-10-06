using System.Globalization;
using System.Text.RegularExpressions;
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

    public Task<LearnPageResult> FetchDeprecatedFeaturesAsync(CancellationToken ct)
    {
        const string reason = "Deprecated-features parsing is not implemented yet.";
        logger.LogWarning("Learn deprecated-features fetch skipped: {Reason}", reason);
        return Task.FromResult(new LearnPageResult(Available: false, Items: [], UnavailableReason: reason));
    }

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

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex RoadmapIdPattern();
}
