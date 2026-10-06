using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Diffing;
using BcReleasePlanPortal.Ingest.Learn;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class LearnTargetVersionsTests
{
    private static LearnPageResult Page(params string[] roadmapIds) =>
        new(true, [.. roadmapIds.Select(id => new LearnPageItem("t", "u", id, null, null, []))], null);

    [Fact]
    public void Picks_the_most_recent_major_updates_only()
    {
        var majors = LearnTargetVersions.MostRecentMajors(["27.0", "27.5", "28.0", "28.1", "9.0", "29.0"], count: 2);

        Assert.Equal(["28.0", "29.0"], majors);
    }

    [Fact]
    public void Later_update_wins_when_an_item_is_listed_on_two()
    {
        var map = LearnTargetVersions.Build([("28.0", Page("1", "2")), ("29.0", Page("2"))]);

        Assert.Equal("28.0", map["1"]);
        Assert.Equal("29.0", map["2"]);
    }

    [Fact]
    public void Preview_rows_do_not_set_a_target_version()
    {
        var page = new LearnPageResult(true,
        [
            new LearnPageItem("ga", "u", "1", null, "General availability", []),
            new LearnPageItem("preview", "u", "2", null, "Public preview", []),
        ], null);

        var map = LearnTargetVersions.Build([("29.0", page)]);

        Assert.Equal("29.0", map["1"]);
        Assert.False(map.ContainsKey("2"));
    }

    [Fact]
    public void Unavailable_pages_contribute_nothing()
    {
        var map = LearnTargetVersions.Build([("29.0", new LearnPageResult(false, [], "down"))]);

        Assert.Empty(map);
    }

    [Theory]
    [InlineData(null, "29.0")]   // Learn unreachable: keep what we had
    [InlineData("other", "29.0")] // not on the pages read: keep what we had
    [InlineData("573365", "29.0")]
    public void Never_clears_a_known_version(string? mappedId, string expected)
    {
        IReadOnlyDictionary<string, string>? map = mappedId switch
        {
            null => null,
            "other" => new Dictionary<string, string> { ["999"] = "28.0" },
            _ => new Dictionary<string, string> { [mappedId] = "29.0" },
        };

        Assert.Equal(expected, LearnTargetVersions.Resolve(map, "573365", knownVersion: "29.0"));
    }

    [Fact]
    public void Moves_a_version_when_Learn_says_so()
    {
        var map = new Dictionary<string, string> { ["573365"] = "30.0" };

        Assert.Equal("30.0", LearnTargetVersions.Resolve(map, "573365", knownVersion: "29.0"));
    }

    [Fact]
    public void First_target_version_is_enrichment_not_a_change_event()
    {
        var events = ChangeEventDetector.Detect(Item(null), Item("29.0"), DateTimeOffset.UtcNow);

        Assert.Empty(events);
    }

    [Fact]
    public void A_target_version_that_moves_is_a_change_event()
    {
        var events = ChangeEventDetector.Detect(Item("29.0"), Item("30.0"), DateTimeOffset.UtcNow);

        var e = Assert.Single(events);
        Assert.Equal(nameof(RoadmapItem.TargetVersion), e.Field);
        Assert.Equal("29.0", e.OldValue);
        Assert.Equal("30.0", e.NewValue);
    }

    private static RoadmapItem Item(string? targetVersion) => new()
    {
        Id = Guid.Empty,
        Source = RoadmapItemSource.Roadmap,
        ExternalId = "573365",
        Product = "bc",
        Title = "Map new Dataverse fields in Business Central",
        TargetVersion = targetVersion,
        PayloadHash = "",
    };
}
