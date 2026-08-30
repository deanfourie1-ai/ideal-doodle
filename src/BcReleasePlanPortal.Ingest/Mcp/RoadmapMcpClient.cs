using System.Text;
using System.Text.Json;
using BcReleasePlanPortal.Ingest.Mcp.Models;

namespace BcReleasePlanPortal.Ingest.Mcp;

public sealed class RoadmapMcpClient(McpJsonRpcClient rpc) : IRoadmapMcpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<McpRoadmapListResult> GetRecentRoadmapsAsync(IReadOnlyCollection<string> productTags, int skip, CancellationToken ct)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["filter"] = BuildProductsAnyFilter(productTags),
        };
        if (skip > 0)
        {
            arguments["skip"] = skip;
        }

        var node = await rpc.CallToolAsync("get_recent_m365_roadmaps", arguments, ct);
        return node.Deserialize<McpRoadmapListResult>(JsonOptions)
            ?? throw new McpToolCallException("get_recent_m365_roadmaps", "Empty result", null);
    }

    public async Task<McpRoadmapItem> GetRoadmapByIdAsync(string id, CancellationToken ct)
    {
        var node = await rpc.CallToolAsync("get_m365_roadmap_by_id", new { id }, ct);
        return node.Deserialize<McpRoadmapItem>(JsonOptions)
            ?? throw new McpToolCallException("get_m365_roadmap_by_id", "Empty result", null);
    }

    /// <summary>
    /// Builds "products/any(p: p eq 'A' or p eq 'B')" per the tool's documented OData filter
    /// grammar (design doc §6 confirms this is the intended query shape; the exact grammar was
    /// read from the tool's own inputSchema description on 2026-08-30).
    /// </summary>
    internal static string BuildProductsAnyFilter(IReadOnlyCollection<string> productTags)
    {
        var clauses = productTags.Select(tag => $"p eq '{EscapeODataString(tag)}'");
        var inner = string.Join(" or ", clauses);
        return $"products/any(p: {inner})";
    }

    private static string EscapeODataString(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
