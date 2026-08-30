using System.Text.Json;
using BcReleasePlanPortal.Ingest.Mcp.Models;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

/// <summary>
/// Deserializes real responses captured live from https://www.microsoft.com/releasecommunications/mcp
/// on 2026-08-30 (see Fixtures/*.json) to lock in the field shapes <see cref="RoadmapMcpClient"/>
/// and <see cref="RoadmapItemNormalizer"/> depend on, since there is no published schema for
/// this MCP server's tool output.
/// </summary>
public class McpResponseParsingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Parses_get_recent_m365_roadmaps_list_result()
    {
        var json = File.ReadAllText("Fixtures/mcp_recent_power_automate.json");
        var result = JsonSerializer.Deserialize<McpRoadmapListResult>(json, JsonOptions)!;

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.False(result.HasMore);

        var first = result.Items[0];
        Assert.Equal(561025, first.Id);
        Assert.Equal("SharePoint: Power Automate trigger and action for forms in SharePoint", first.Title);
        Assert.Contains("Power Automate", first.Products);
        Assert.Equal("2026-06", first.GeneralAvailabilityDate);
        Assert.Equal("2026-05", first.PreviewAvailabilityDate);
        Assert.Equal("In development", first.Status);
        Assert.EndsWith("...", first.Description); // list results are truncated (design doc §6 step 1)
    }

    [Fact]
    public void Parses_get_m365_roadmap_by_id_hydrated_item()
    {
        var json = File.ReadAllText("Fixtures/mcp_item_561025.json");
        var item = JsonSerializer.Deserialize<McpRoadmapItem>(json, JsonOptions)!;

        Assert.Equal(561025, item.Id);
        Assert.DoesNotContain("...", item.Description); // hydrated: full, untruncated description
        Assert.True(item.Description.Length > 500);
        Assert.Equal(2026, item.Created?.Year);
    }
}
