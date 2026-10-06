using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Diffing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class TriageCarryOverTests
{
    private static RoadmapItem Item(RoadmapChangeType changeType, string[] modules, DateTimeOffset? triagedAt = null) => new()
    {
        ExternalId = "573341",
        Product = "bc",
        Title = "Ecommerce - Manage Shopify B2B companies, catalogs, and pricing",
        ChangeType = changeType,
        Modules = [.. modules],
        NeedsConfirmation = triagedAt is null,
        TriagedAt = triagedAt,
        PayloadHash = "",
    };

    [Fact]
    public void Untriaged_items_take_the_fresh_classification()
    {
        var existing = Item(RoadmapChangeType.Enhancement, []);
        var current = Item(RoadmapChangeType.NewCapability, ["Sales"]);

        Assert.False(TriageCarryOver.Apply(existing, current));
        Assert.Equal(RoadmapChangeType.NewCapability, current.ChangeType);
        Assert.Equal(["Sales"], current.Modules);
    }

    [Fact]
    public void A_triaged_item_keeps_the_human_change_type_and_modules()
    {
        var triagedAt = DateTimeOffset.Parse("2026-10-06T20:00:00Z");
        var existing = Item(RoadmapChangeType.NewCapability, ["Sales"], triagedAt);
        var current = Item(RoadmapChangeType.Enhancement, []); // what the classifiers say on re-ingest

        Assert.False(TriageCarryOver.Apply(existing, current));
        Assert.Equal(RoadmapChangeType.NewCapability, current.ChangeType);
        Assert.Equal(["Sales"], current.Modules);
        Assert.False(current.NeedsConfirmation);
        Assert.Equal(triagedAt, current.TriagedAt);
        Assert.Equal(PayloadHasher.Compute(current), current.PayloadHash);
    }

    [Fact]
    public void Carried_over_item_produces_no_change_events_for_the_human_fields()
    {
        var existing = Item(RoadmapChangeType.NewCapability, ["Sales"], DateTimeOffset.UtcNow);
        var current = Item(RoadmapChangeType.Enhancement, []);

        TriageCarryOver.Apply(existing, current);

        Assert.Empty(ChangeEventDetector.Detect(existing, current, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(RoadmapChangeType.Deprecation)]
    [InlineData(RoadmapChangeType.Retirement)]
    [InlineData(RoadmapChangeType.BreakingChange)]
    public void Turning_urgent_sends_a_triaged_item_back_to_the_queue(RoadmapChangeType urgent)
    {
        var existing = Item(RoadmapChangeType.Enhancement, ["Sales"], DateTimeOffset.UtcNow);
        var current = Item(urgent, []);

        Assert.True(TriageCarryOver.Apply(existing, current));
        Assert.Equal(urgent, current.ChangeType);
        Assert.Equal(["Sales"], current.Modules); // the human's modules are still the best guess
        Assert.True(current.NeedsConfirmation);
        Assert.Null(current.TriagedAt);

        var changeTypeEvent = Assert.Single(ChangeEventDetector.Detect(existing, current, DateTimeOffset.UtcNow));
        Assert.Equal(nameof(RoadmapItem.ChangeType), changeTypeEvent.Field);
    }

    [Fact]
    public void A_human_confirmed_urgent_type_is_not_sent_back_again()
    {
        var existing = Item(RoadmapChangeType.Retirement, ["Finance"], DateTimeOffset.UtcNow);
        var current = Item(RoadmapChangeType.Retirement, []);

        Assert.False(TriageCarryOver.Apply(existing, current));
        Assert.False(current.NeedsConfirmation);
    }
}

public sealed class RoadmapTriageServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly TestContextFactory _factory;
    private readonly RoadmapTriageService _service;
    private readonly Guid _itemId = Guid.NewGuid();

    public RoadmapTriageServiceTests()
    {
        _connection.Open();
        _factory = new TestContextFactory(new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options);
        using var db = _factory.CreateDbContext();
        db.Database.Migrate();
        db.RoadmapItems.Add(new RoadmapItem
        {
            Id = _itemId,
            ExternalId = "573341",
            Product = "bc",
            Title = "Ecommerce - Manage Shopify B2B companies, catalogs, and pricing",
            ChangeType = RoadmapChangeType.Enhancement,
            NeedsConfirmation = true,
            PayloadHash = "x",
        });
        db.SaveChanges();
        _service = new RoadmapTriageService(_factory, TimeProvider.System);
    }

    private RoadmapItem Load()
    {
        using var db = _factory.CreateDbContext();
        return db.RoadmapItems.AsNoTracking().Single(i => i.Id == _itemId);
    }

    [Fact]
    public async Task Confirm_clears_the_flag_and_stamps_the_time()
    {
        await _service.ConfirmAsync(_itemId);

        var item = Load();
        Assert.False(item.NeedsConfirmation);
        Assert.NotNull(item.TriagedAt);
        Assert.Equal(RoadmapChangeType.Enhancement, item.ChangeType);
    }

    [Fact]
    public async Task Correct_stores_the_human_values_in_taxonomy_order()
    {
        await _service.CorrectAsync(_itemId, RoadmapChangeType.NewCapability, ["Sales", "Finance"]);

        var item = Load();
        Assert.Equal(RoadmapChangeType.NewCapability, item.ChangeType);
        Assert.Equal(["Finance", "Sales"], item.Modules);
        Assert.NotNull(item.TriagedAt);
        Assert.False(item.NeedsConfirmation);
    }

    [Fact]
    public async Task Reopen_puts_the_item_back_in_the_queue()
    {
        await _service.ConfirmAsync(_itemId);
        await _service.ReopenAsync(_itemId);

        var item = Load();
        Assert.Null(item.TriagedAt);
        Assert.True(item.NeedsConfirmation);
    }

    [Fact]
    public async Task Rejects_a_module_outside_the_taxonomy()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CorrectAsync(_itemId, RoadmapChangeType.Enhancement, ["Ecommerce"]));
        Assert.True(Load().NeedsConfirmation);
    }

    [Fact]
    public async Task Rejects_an_unknown_change_type()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CorrectAsync(_itemId, RoadmapChangeType.Unknown, []));
    }

    public void Dispose() => _connection.Dispose();

    private sealed class TestContextFactory(DbContextOptions<BcReleasePlanDbContext> options) : IDbContextFactory<BcReleasePlanDbContext>
    {
        public BcReleasePlanDbContext CreateDbContext() => new(options);
    }
}

public class BcModulesTests
{
    [Fact]
    public void Classifier_keywords_cover_exactly_the_module_taxonomy()
    {
        var classifierModules = typeof(BcReleasePlanPortal.Ingest.Normalization.RuleBasedModuleClassifier)
            .GetField("ModuleKeywords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null) as Dictionary<string, string[]>;

        Assert.Equal(BcModules.All.Order(), classifierModules!.Keys.Order());
    }
}
