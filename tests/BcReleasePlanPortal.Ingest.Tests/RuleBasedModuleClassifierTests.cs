using BcReleasePlanPortal.Ingest.Normalization;
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
