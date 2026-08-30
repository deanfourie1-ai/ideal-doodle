using System.Text.Json.Serialization;

namespace BcReleasePlanPortal.Ingest.Mcp.Models;

/// <summary>
/// Shape of one item returned by the get_recent_m365_roadmaps / get_m365_roadmap_by_id MCP
/// tools, as observed live 2026-08-30. get_recent_* returns these with a truncated
/// <see cref="Description"/> (doc §6 step 1); get_m365_roadmap_by_id returns the same shape
/// with the full description (doc §6 step 2, "hydrate").
/// </summary>
public sealed class McpRoadmapItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("baseId")]
    public long BaseId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("products")]
    public List<string> Products { get; set; } = [];

    [JsonPropertyName("platforms")]
    public List<string> Platforms { get; set; } = [];

    [JsonPropertyName("cloudInstances")]
    public List<string> CloudInstances { get; set; } = [];

    [JsonPropertyName("releaseRings")]
    public List<string> ReleaseRings { get; set; } = [];

    [JsonPropertyName("moreInfoUrls")]
    public List<string> MoreInfoUrls { get; set; } = [];

    [JsonPropertyName("availabilities")]
    public List<McpAvailability> Availabilities { get; set; } = [];

    /// <summary>"YYYY-MM", nullable.</summary>
    [JsonPropertyName("generalAvailabilityDate")]
    public string? GeneralAvailabilityDate { get; set; }

    /// <summary>"YYYY-MM", nullable.</summary>
    [JsonPropertyName("previewAvailabilityDate")]
    public string? PreviewAvailabilityDate { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; set; }

    [JsonPropertyName("modified")]
    public DateTimeOffset? Modified { get; set; }
}

public sealed class McpAvailability
{
    [JsonPropertyName("ring")]
    public string Ring { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;
}

public sealed class McpRoadmapListResult
{
    [JsonPropertyName("items")]
    public List<McpRoadmapItem> Items { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }

    [JsonPropertyName("returnedCount")]
    public int ReturnedCount { get; set; }
}
