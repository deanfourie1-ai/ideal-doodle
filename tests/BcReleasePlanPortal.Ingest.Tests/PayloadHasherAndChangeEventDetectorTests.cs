using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Diffing;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class PayloadHasherAndChangeEventDetectorTests
{
    private static RoadmapItem MakeItem(DateOnly? gaDate = null, string title = "Remove feature key: SOAP publishing for standard pages") => new()
    {
        Id = Guid.NewGuid(),
        Source = RoadmapItemSource.Roadmap,
        ExternalId = "118342",
        Product = "bc",
        Title = title,
        DescriptionRaw = "Microsoft is removing the feature key for SOAP publishing.",
        Modules = ["Dev/API"],
        ChangeType = RoadmapChangeType.Deprecation,
        TargetVersion = "29.0",
        GaDate = gaDate ?? new DateOnly(2026, 10, 1),
        Status = RoadmapItemStatus.Planned,
        PayloadHash = string.Empty,
    };

    [Fact]
    public void Hash_is_stable_for_identical_content()
    {
        var a = MakeItem();
        var b = MakeItem();

        Assert.Equal(PayloadHasher.Compute(a), PayloadHasher.Compute(b));
    }

    [Fact]
    public void Hash_changes_when_GA_date_moves()
    {
        // This is the scenario the whole ChangeEvent table exists for (design doc §5.2):
        // "A GA date sliding from October to April changes a customer's plan, and under the
        // continuous model nobody announces it. Your daily diff catches it."
        var october = MakeItem(gaDate: new DateOnly(2026, 10, 1));
        var april = MakeItem(gaDate: new DateOnly(2027, 4, 1));

        Assert.NotEqual(PayloadHasher.Compute(october), PayloadHasher.Compute(april));
    }

    [Fact]
    public void Detect_emits_a_GaDate_event_with_old_and_new_values()
    {
        var previous = MakeItem(gaDate: new DateOnly(2026, 10, 1));
        var current = MakeItem(gaDate: new DateOnly(2027, 4, 1));

        var events = ChangeEventDetector.Detect(previous, current, DateTimeOffset.UtcNow);

        var gaDateEvent = Assert.Single(events, e => e.Field == nameof(RoadmapItem.GaDate));
        Assert.Equal("2026-10-01", gaDateEvent.OldValue);
        Assert.Equal("2027-04-01", gaDateEvent.NewValue);
    }

    [Fact]
    public void Detect_emits_nothing_when_content_is_unchanged()
    {
        var previous = MakeItem();
        var current = MakeItem();

        var events = ChangeEventDetector.Detect(previous, current, DateTimeOffset.UtcNow);

        Assert.Empty(events);
    }

    [Fact]
    public void RequiresImmediateAlert_true_for_a_new_deprecation_classification()
    {
        var previous = MakeItem();
        previous.ChangeType = RoadmapChangeType.Enhancement;
        var current = MakeItem();
        current.ChangeType = RoadmapChangeType.Deprecation;

        var events = ChangeEventDetector.Detect(previous, current, DateTimeOffset.UtcNow);

        Assert.True(ChangeEventDetector.RequiresImmediateAlert(current, events, alreadyPublished: false));
    }

    [Fact]
    public void RequiresImmediateAlert_true_for_a_GA_date_move_on_a_published_item()
    {
        var previous = MakeItem(gaDate: new DateOnly(2026, 10, 1));
        previous.ChangeType = RoadmapChangeType.Enhancement;
        var current = MakeItem(gaDate: new DateOnly(2027, 4, 1));
        current.ChangeType = RoadmapChangeType.Enhancement;

        var events = ChangeEventDetector.Detect(previous, current, DateTimeOffset.UtcNow);

        Assert.True(ChangeEventDetector.RequiresImmediateAlert(current, events, alreadyPublished: true));
        Assert.False(ChangeEventDetector.RequiresImmediateAlert(current, events, alreadyPublished: false));
    }

    [Fact]
    public void RequiresImmediateAlert_false_for_an_unpublished_enhancement_tweak()
    {
        var previous = MakeItem(title: "Warehouse tweak");
        previous.ChangeType = RoadmapChangeType.Enhancement;
        var current = MakeItem(title: "Warehouse tweak, revised copy");
        current.ChangeType = RoadmapChangeType.Enhancement;

        var events = ChangeEventDetector.Detect(previous, current, DateTimeOffset.UtcNow);

        Assert.NotEmpty(events);
        Assert.False(ChangeEventDetector.RequiresImmediateAlert(current, events, alreadyPublished: false));
    }
}
