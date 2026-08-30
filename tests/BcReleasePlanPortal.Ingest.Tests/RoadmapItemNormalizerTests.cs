using System.Text.Json;
using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Mcp.Models;
using BcReleasePlanPortal.Ingest.Normalization;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class RoadmapItemNormalizerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RoadmapItemNormalizer _normalizer = new(new RuleBasedChangeClassifier(), new RuleBasedModuleClassifier());

    [Fact]
    public void Normalizes_a_real_hydrated_MCP_item()
    {
        var json = File.ReadAllText("Fixtures/mcp_item_561025.json");
        var source = JsonSerializer.Deserialize<McpRoadmapItem>(json, JsonOptions)!;
        var now = DateTimeOffset.Parse("2026-08-30T00:00:00Z");

        var item = _normalizer.Normalize(source, "power_automate_demo", now);

        Assert.Equal("561025", item.ExternalId);
        Assert.Equal("power_automate_demo", item.Product);
        Assert.Equal(RoadmapItemSource.Roadmap, item.Source);
        Assert.Equal(RoadmapItemStatus.Planned, item.Status); // "In development"
        Assert.Equal(new DateOnly(2026, 6, 1), item.GaDate);
        Assert.Equal(new DateOnly(2026, 5, 1), item.PreviewDate);
        Assert.Equal(source.Modified, item.SourceModifiedAt);
        Assert.Equal(now, item.FirstSeenAt);
        Assert.False(string.IsNullOrEmpty(item.PayloadHash));

        // These are BC-specific fields with no source on the M365 roadmap MCP surface today.
        Assert.Null(item.TargetVersion);
        Assert.Empty(item.ObjectsTouched);
        Assert.Equal(RoadmapEnabledBy.Unknown, item.EnabledBy);
    }

    [Fact]
    public void GA_only_year_month_parses_to_first_of_month()
    {
        var source = new McpRoadmapItem { Id = 1, Title = "x", Description = "y", GeneralAvailabilityDate = "2027-04" };

        var item = _normalizer.Normalize(source, "bc", DateTimeOffset.UtcNow);

        Assert.Equal(new DateOnly(2027, 4, 1), item.GaDate);
    }

    [Fact]
    public void Missing_availability_dates_stay_null_rather_than_defaulting()
    {
        var source = new McpRoadmapItem { Id = 1, Title = "x", Description = "y" };

        var item = _normalizer.Normalize(source, "bc", DateTimeOffset.UtcNow);

        Assert.Null(item.GaDate);
        Assert.Null(item.PreviewDate);
    }
}
