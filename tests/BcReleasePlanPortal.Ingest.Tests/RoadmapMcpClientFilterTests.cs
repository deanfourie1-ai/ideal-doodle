using BcReleasePlanPortal.Ingest.Mcp;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

/// <summary>
/// Covers the OData filter string built for get_recent_m365_roadmaps, per the grammar
/// documented in the tool's own inputSchema description (read live from the MCP server's
/// tools/list response on 2026-08-30 — see the "Basic filters: Products" example there).
/// </summary>
public class RoadmapMcpClientFilterTests
{
    [Fact]
    public void Builds_any_filter_for_a_single_product_tag()
    {
        var filter = RoadmapMcpClient.BuildProductsAnyFilter(["Dynamics 365 Business Central"]);

        Assert.Equal("products/any(p: p eq 'Dynamics 365 Business Central')", filter);
    }

    [Fact]
    public void Ors_multiple_product_tags_inside_one_any_clause()
    {
        var filter = RoadmapMcpClient.BuildProductsAnyFilter(["Power Automate", "SharePoint"]);

        Assert.Equal("products/any(p: p eq 'Power Automate' or p eq 'SharePoint')", filter);
    }

    [Fact]
    public void Escapes_single_quotes_in_product_tags_for_the_OData_string_literal()
    {
        var filter = RoadmapMcpClient.BuildProductsAnyFilter(["Contoso's Product"]);

        Assert.Equal("products/any(p: p eq 'Contoso''s Product')", filter);
    }
}
