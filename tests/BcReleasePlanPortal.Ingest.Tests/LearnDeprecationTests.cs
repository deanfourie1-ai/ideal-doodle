using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Learn;
using BcReleasePlanPortal.Ingest.Normalization;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

/// <summary>Against the real deprecated-features page captured live on 2026-10-06.</summary>
public class LearnDeprecationTests
{
    private const string PageUrl = "https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/upgrade/deprecated-features-w1";

    private static IReadOnlyList<LearnDeprecation> Parse() =>
        HttpLearnPageSource.ParseDeprecatedFeatures(File.ReadAllText("Fixtures/learn_deprecated_features_w1.html"), PageUrl).Items;

    [Fact]
    public void Parses_every_feature_section_back_to_2021()
    {
        var items = Parse();

        Assert.Equal(33, items.Count);
        Assert.Contains(items, i => i.WaveYear == 2021);
    }

    [Fact]
    public void Takes_the_version_from_a_wave_heading_that_prints_it()
    {
        var amc = Assert.Single(Parse(), i => i.Title == "AMC Fundamentals app (removal)");

        Assert.Equal("30.0", amc.Version);
        Assert.Equal((2027, 1), (amc.WaveYear, amc.WaveNumber));
        Assert.Equal("Replaced", amc.State);
        Assert.Equal(PageUrl + "#amc-fundamentals-app-removal", amc.Url);
        Assert.Contains("AMC-owned app", amc.Description);
    }

    [Fact]
    public void Derives_the_version_when_the_wave_heading_omits_it()
    {
        var warning = Assert.Single(Parse(), i => i.Title == "Finance reports API (beta) (warning)");

        Assert.Equal("29.0", warning.Version);
    }

    [Theory]
    [InlineData(2022, 2, "21.0")]
    [InlineData(2026, 1, "28.0")]
    [InlineData(2026, 2, "29.0")]
    [InlineData(2027, 1, "30.0")]
    public void Wave_to_version_matches_the_versions_Microsoft_prints(int year, int wave, string expected)
    {
        Assert.Equal(expected, HttpLearnPageSource.VersionForWave(year, wave));
    }

    [Fact]
    public void Folds_a_multi_row_section_into_one_item()
    {
        var packages = Assert.Single(Parse(), i => i.Title.StartsWith("Configuration packages", StringComparison.Ordinal));

        Assert.Contains("\n\n", packages.Description);
    }

    [Fact]
    public void Page_without_wave_sections_is_reported_unavailable()
    {
        var result = HttpLearnPageSource.ParseDeprecatedFeatures("<html><body><main><h2>Something else</h2></main></body></html>", PageUrl);

        Assert.False(result.Available);
    }

    [Theory]
    [InlineData("Finance reports API (beta) (removal)", "Replaced", RoadmapChangeType.Retirement)]
    [InlineData("Finance reports API (beta) (warning)", "Replaced", RoadmapChangeType.Deprecation)]
    [InlineData("XBRL reporting", "Removed", RoadmapChangeType.Retirement)]
    [InlineData("Subcontracting Worksheet moved to the Subcontracting app", "Moved", RoadmapChangeType.BehaviourChange)]
    [InlineData("Peppol BIS 2.0 and Peppol BIS 2.1 are replaced by Peppol BIS 3.0", "Replaced", RoadmapChangeType.Deprecation)]
    public void Change_type_comes_from_the_pages_own_wording(string title, string state, RoadmapChangeType expected)
    {
        var source = new LearnDeprecation(title, PageUrl, 2027, 1, "30.0", state, "");

        Assert.Equal(expected, LearnDeprecationNormalizer.ClassifyChange(source));
    }

    [Fact]
    public void Normalizes_to_a_confident_planned_item_dated_to_its_wave()
    {
        var source = new LearnDeprecation("Finance reports API (beta) (removal)", PageUrl + "#x", 2027, 1, "30.0", "Replaced", "Use the Analytics API instead.");

        var item = new LearnDeprecationNormalizer(new RuleBasedModuleClassifier()).Normalize(source, "bc", currentMajorVersion: 29, DateTimeOffset.UtcNow);

        Assert.Equal(RoadmapItemSource.LearnDeprecation, item.Source);
        Assert.Equal("30.0/finance-reports-api-beta-removal", item.ExternalId);
        Assert.Equal(RoadmapChangeType.Retirement, item.ChangeType);
        Assert.Equal(RoadmapItemStatus.Planned, item.Status);
        Assert.Equal("30.0", item.TargetVersion);
        Assert.Equal(new DateOnly(2027, 4, 1), item.GaDate);
        Assert.Contains("Dev/API", item.Modules);
        Assert.False(item.NeedsConfirmation);
    }

    [Fact]
    public void The_same_feature_warning_and_removal_are_separate_items()
    {
        var items = Parse().Where(i => i.Title.StartsWith("Finance reports API", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, items.Select(LearnDeprecationNormalizer.ExternalIdFor).Distinct().Count());
    }
}
