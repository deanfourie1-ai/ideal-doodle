using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Normalization;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class RuleBasedChangeClassifierTests
{
    private readonly RuleBasedChangeClassifier _classifier = new();

    [Fact]
    public void Classifies_the_SOAP_deprecation_scenario_from_the_design_doc()
    {
        // Design doc §7: "Microsoft removes the feature key that re-enables SOAP publishing
        // for standard UI pages in version 29."
        var result = _classifier.Classify(
            title: "Remove feature key: SOAP publishing for standard pages",
            description: "Microsoft is removing the feature key that allows SOAP publishing on standard pages. This capability will be deprecated in version 29.");

        Assert.Equal(RoadmapChangeType.Deprecation, result.ChangeType);
        Assert.True(result.Confident);
    }

    [Fact]
    public void Classifies_retirement_keyword_as_retirement_not_deprecation()
    {
        var result = _classifier.Classify(
            title: "Retire legacy Statistics report objects",
            description: "These report objects are being retired and will no longer be available.");

        Assert.Equal(RoadmapChangeType.Retirement, result.ChangeType);
        Assert.True(result.Confident);
    }

    [Fact]
    public void Classifies_explicit_breaking_change_language()
    {
        var result = _classifier.Classify(
            title: "API v2.0 sandbox rate limit change",
            description: "This is a breaking change for integrations relying on the previous limit.");

        Assert.Equal(RoadmapChangeType.BreakingChange, result.ChangeType);
        Assert.True(result.Confident);
    }

    [Fact]
    public void Falls_back_to_low_confidence_enhancement_when_no_keyword_matches()
    {
        var result = _classifier.Classify(
            title: "Warehouse: license plate posting performance improvements",
            description: "Posting license plates is now faster in high-volume warehouses.");

        Assert.Equal(RoadmapChangeType.Enhancement, result.ChangeType);
        Assert.False(result.Confident); // must be confirmed by a human during triage — design doc §8 Screen 1
    }

    [Fact]
    public void Real_power_automate_fixture_text_is_not_confidently_classified()
    {
        // Real, unremarkable roadmap copy shouldn't accidentally trip an urgent-category keyword.
        var result = _classifier.Classify(
            title: "SharePoint: Power Automate trigger and action for forms in SharePoint",
            description: "We are introducing new Power Automate integration capabilities for forms in SharePoint.");

        Assert.NotEqual(RoadmapChangeType.Deprecation, result.ChangeType);
        Assert.NotEqual(RoadmapChangeType.BreakingChange, result.ChangeType);
        Assert.NotEqual(RoadmapChangeType.Retirement, result.ChangeType);
    }
}
