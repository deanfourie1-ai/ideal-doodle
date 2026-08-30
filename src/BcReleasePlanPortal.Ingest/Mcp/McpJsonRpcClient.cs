using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BcReleasePlanPortal.Ingest.Mcp;

/// <summary>
/// Minimal JSON-RPC 2.0 client for an MCP server exposed over "streamable HTTP" (single POST
/// per call, no session negotiation observed on this server). Verified 2026-08-30 against
/// https://www.microsoft.com/releasecommunications/mcp: it is POST-only (a GET returns 405),
/// requires no auth, and replies with a single SSE frame — "event: message\ndata: {json}\n\n" —
/// even though the request declared it would also accept application/json. This class handles
/// both response shapes so it isn't tied to that implementation detail.
/// </summary>
public sealed class McpJsonRpcClient(HttpClient httpClient)
{
    private int _nextId;

    public async Task<JsonNode> CallToolAsync(string toolName, object? arguments, CancellationToken ct)
    {
        var requestBody = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref _nextId),
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = arguments is null ? new JsonObject() : JsonSerializer.SerializeToNode(arguments),
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var envelope = ParseJsonRpcEnvelope(body);

        if (envelope["error"] is JsonObject error)
        {
            throw new McpToolCallException(toolName, error["message"]?.GetValue<string>() ?? "MCP tool call failed", error);
        }

        var result = envelope["result"] ?? throw new McpToolCallException(toolName, "MCP response had neither 'result' nor 'error'", null);

        // Tool results carry their payload as a text-typed content block whose text is itself
        // a JSON document (observed shape for get_recent_m365_roadmaps / get_m365_roadmap_by_id).
        var text = result["content"]?.AsArray()
            .Select(c => c?["type"]?.GetValue<string>() == "text" ? c["text"]?.GetValue<string>() : null)
            .FirstOrDefault(t => t is not null);

        if (text is null)
        {
            throw new McpToolCallException(toolName, "MCP tool result had no text content block", result);
        }

        return JsonNode.Parse(text) ?? throw new McpToolCallException(toolName, "MCP tool result text was not valid JSON", result);
    }

    private static JsonObject ParseJsonRpcEnvelope(string body)
    {
        // SSE framing: one or more "event: ...\ndata: {...}\n\n" blocks. We only need the last
        // data line, which carries the JSON-RPC envelope for this request/response pair.
        var dataLine = body
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .LastOrDefault(l => l.StartsWith("data:", StringComparison.Ordinal));

        var jsonText = dataLine is null ? body : dataLine["data:".Length..].Trim();

        return JsonNode.Parse(jsonText) as JsonObject
            ?? throw new McpToolCallException("(unknown)", "MCP response body was not a JSON object", null);
    }
}

public sealed class McpToolCallException(string toolName, string message, JsonNode? errorPayload)
    : Exception($"MCP tool '{toolName}' failed: {message}")
{
    public string ToolName { get; } = toolName;
    public JsonNode? ErrorPayload { get; } = errorPayload;
}
