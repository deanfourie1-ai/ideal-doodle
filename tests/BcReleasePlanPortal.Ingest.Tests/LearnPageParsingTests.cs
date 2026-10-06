using BcReleasePlanPortal.Ingest.Learn;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

/// <summary>
/// Against real Learn pages captured live on 2026-10-06 (Fixtures/learn_*), the first day
/// learn.microsoft.com was reachable from a dev machine — so these lock in observed markup, not
/// remembered markup.
/// </summary>
public class LearnPageParsingTests
{
    private const string PageUrl = "https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/whatsnew/whatsnew-update-29-0";

    [Fact]
    public void Parses_every_row_of_the_update_29_feature_table_with_its_roadmap_id()
    {
        var result = HttpLearnPageSource.ParseWhatsNew(File.ReadAllText("Fixtures/learn_whatsnew_update_29_0.html"), PageUrl);

        Assert.True(result.Available);
        // All 69 items the MCP roadmap listed as Launched on 2026-10-06 have GA in 29.0; the page
        // must at least cover those, every row with a numeric Roadmap ID.
        Assert.True(result.Items.Count >= 69, $"only {result.Items.Count} rows parsed");
        Assert.All(result.Items, item => Assert.Matches(@"^\d+$", item.RoadmapId!));
    }

    [Fact]
    public void Carries_the_product_area_down_to_rows_that_leave_it_blank()
    {
        var result = HttpLearnPageSource.ParseWhatsNew(File.ReadAllText("Fixtures/learn_whatsnew_update_29_0.html"), PageUrl);

        // Second row of the "Copilot and agents" group; its Product area cell is empty on the page.
        var item = Assert.Single(result.Items, i => i.RoadmapId == "573380");
        Assert.Equal("Copilot and agents", item.ProductArea);
        Assert.Equal("Improve purchase order matching in Payables Agent", item.Title);
        Assert.Equal("General availability", item.Availability);
    }

    [Fact]
    public void Minor_update_page_parses_but_yields_no_roadmap_ids()
    {
        var result = HttpLearnPageSource.ParseWhatsNew(File.ReadAllText("Fixtures/learn_whatsnew_update_28_1.html"), PageUrl);

        Assert.True(result.Available);
        Assert.All(result.Items, item => Assert.Null(item.RoadmapId));
    }

    [Fact]
    public void Page_without_a_feature_table_is_reported_unavailable_not_empty()
    {
        var result = HttpLearnPageSource.ParseWhatsNew("<html><body><table><tr><td>x</td></tr></table></body></html>", PageUrl);

        Assert.False(result.Available);
        Assert.NotNull(result.UnavailableReason);
    }

    [Fact]
    public void Discovers_update_versions_from_the_table_of_contents()
    {
        var versions = HttpLearnPageSource.ParseTocVersions(File.ReadAllText("Fixtures/learn_devitpro_toc.json"));

        Assert.Contains("28.0", versions);
        Assert.Contains("28.5", versions);
        Assert.Equal("29.0", versions[^1]);
    }

    [Fact]
    public void Orders_versions_numerically_not_alphabetically()
    {
        var versions = HttpLearnPageSource.ParseTocVersions("whatsnew-update-9-0 whatsnew-update-10-0 whatsnew-update-9-10 whatsnew-update-9-2");

        Assert.Equal(["9.0", "9.2", "9.10", "10.0"], versions);
    }

    [Fact]
    public void Builds_whats_new_path_from_a_version()
    {
        Assert.Equal("whatsnew/whatsnew-update-29-0", HttpLearnPageSource.WhatsNewPath("29.0"));
    }
}
