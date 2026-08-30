using BcReleasePlanPortal.Ingest.Mcp.Models;

namespace BcReleasePlanPortal.Ingest.Mcp;

public interface IRoadmapMcpClient
{
    /// <summary>
    /// Calls get_recent_m365_roadmaps. <paramref name="productTags"/> becomes an OData
    /// "products/any(...)" filter (OR'd together) — see design doc §6 step 1. Descriptions
    /// come back truncated; call <see cref="GetRoadmapByIdAsync"/> for new/changed items
    /// (doc §6 step 2, "hydrate").
    /// </summary>
    Task<McpRoadmapListResult> GetRecentRoadmapsAsync(IReadOnlyCollection<string> productTags, int skip, CancellationToken ct);

    Task<McpRoadmapItem> GetRoadmapByIdAsync(string id, CancellationToken ct);
}
