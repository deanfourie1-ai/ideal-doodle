using BcReleasePlanPortal.Ingest.Normalization;
using System.Text.Json;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class RuleBasedModuleClassifierTests
{
    private readonly RuleBasedModuleClassifier _classifier = new();

    [Fact]
    public void Matches_Dev_API_module_for_SOAP_deprecation_scenario()
    {
        var result = _classifier.Classify(
            title: "Remove feature key: SOAP publishing for standard pages",
            description: "Removes the SOAP web service endpoint feature key.",
            microsoftProductTags: []);

        Assert.Contains("Dev/API", result.Modules);
        Assert.True(result.Confident);
    }

    [Fact]
    public void Matches_Warehouse_module()
    {
        var result = _classifier.Classify(
            title: "Warehouse: license plate posting performance improvements",
            description: "Faster license plate posting for high-volume warehouses.",
            microsoftProductTags: []);

        Assert.Contains("Warehouse", result.Modules);
    }

    [Fact]
    public void Can_match_more_than_one_module()
    {
        var result = _classifier.Classify(
            title: "Purchase order approval workflow: delegation behaviour change",
            description: "Approval workflow changes for vendor purchase orders.",
            microsoftProductTags: []);

        Assert.Contains("Purchasing", result.Modules);
    }

    // Real BC items captured live on 2026-10-06 (Fixtures/mcp_item_5732xx.json) that the first
    // ingest of BC data tagged Localisation-NL: "sepa" matched inside "separate", and "Dutch"
    // appeared only in a list of supported UI languages.
    [Theory]
    [InlineData("Fixtures/mcp_item_573306.json")]
    [InlineData("Fixtures/mcp_item_573253.json")]
    public void Does_not_tag_Localisation_NL_from_words_that_merely_contain_a_keyword(string fixture)
    {
        using var item = JsonDocument.Parse(File.ReadAllText(fixture));
        var result = _classifier.Classify(
            title: item.RootElement.GetProperty("title").GetString()!,
            description: item.RootElement.GetProperty("description").GetString()!,
            microsoftProductTags: ["Dynamics 365 Business Central"]);

        Assert.DoesNotContain("Localisation-NL", result.Modules);
    }

    [Theory]
    [InlineData("Generate SEPA credit transfer files from payment journals")]
    [InlineData("Netherlands: submit BTW returns electronically")]
    [InlineData("Updates to the Dutch localization for intrastat")]
    public void Still_tags_Localisation_NL_for_genuine_Dutch_localisation_items(string title)
    {
        var result = _classifier.Classify(title, description: "", microsoftProductTags: []);

        Assert.Contains("Localisation-NL", result.Modules);
    }

    [Theory]
    [InlineData("Reporting and data analysis - Bookmark list views", "Reporting")]
    [InlineData("Work more efficiently with manufacturing documents", "Manufacturing")]
    [InlineData("Use inventory put-aways and picks for subcontracting", "Warehouse")]
    [InlineData("Expose new APIs for agents", "Dev/API")]
    public void Matches_inflected_forms_of_a_keyword(string title, string expectedModule)
    {
        var result = _classifier.Classify(title, description: "", microsoftProductTags: []);

        Assert.Contains(expectedModule, result.Modules);
    }

    [Theory]
    [InlineData("Improved capability for rapid onboarding", "Dev/API")]
    [InlineData("Combine binary attachments", "Warehouse")]
    public void Does_not_match_a_keyword_inside_an_unrelated_word(string title, string unexpectedModule)
    {
        var result = _classifier.Classify(title, description: "", microsoftProductTags: []);

        Assert.DoesNotContain(unexpectedModule, result.Modules);
    }

    [Fact]
    public void Generic_mention_of_customers_does_not_tag_Sales()
    {
        // Real Learn deprecation text (AMC Fundamentals removal, 2027 wave 1) — a banking app.
        var result = _classifier.Classify(
            title: "AMC Fundamentals app (removal)",
            description: "Customers who use AMC Fundamentals and wish to use AMC's new app must coordinate this migration with AMC.",
            microsoftProductTags: []);

        Assert.DoesNotContain("Sales", result.Modules);
    }

    [Theory]
    [InlineData("Ecommerce - Control sales document creation for Shopify orders and returns")]
    [InlineData("Show balance on the customer card")]
    public void Specific_sales_terms_still_tag_Sales(string title)
    {
        var result = _classifier.Classify(title, description: "", microsoftProductTags: []);

        Assert.Contains("Sales", result.Modules);
    }

    [Fact]
    public void Returns_no_modules_and_low_confidence_when_nothing_matches()
    {
        var result = _classifier.Classify(
            title: "Something entirely unrelated to BC",
            description: "No domain keywords here at all.",
            microsoftProductTags: ["Power Automate"]);

        Assert.Empty(result.Modules);
        Assert.False(result.Confident);
    }
}
