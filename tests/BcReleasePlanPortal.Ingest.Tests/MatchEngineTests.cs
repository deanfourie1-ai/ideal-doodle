using System.Text.Json;
using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Matching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

/// <summary>Scorer rules, exercised against the two sample profiles and real item titles.</summary>
public class MatchScorerTests
{
    private static readonly List<Customer> Samples = JsonSerializer.Deserialize<List<Customer>>(File.ReadAllText("Samples/customers.sample.json"))!;
    private static Customer Production => Samples.Single(c => c.Name.StartsWith("Brightline"));
    private static Customer Distribution => Samples.Single(c => c.Name.StartsWith("Northgate"));

    private static RoadmapItem Item(string title, RoadmapChangeType type = RoadmapChangeType.Enhancement, string[]? modules = null, bool triaged = false, string? version = null) => new()
    {
        ExternalId = "x",
        Product = "bc",
        Title = title,
        ChangeType = type,
        Modules = [.. modules ?? []],
        NeedsConfirmation = !triaged,
        TriagedAt = triaged ? DateTimeOffset.UtcNow : null,
        TargetVersion = version,
        PayloadHash = "",
    };

    [Fact]
    public void Installed_app_being_removed_is_a_strong_candidate_for_that_customer_only()
    {
        var amc = Item("AMC Fundamentals app (removal)", RoadmapChangeType.Retirement, version: "30.0");

        var production = MatchScorer.Score(amc, Production);
        Assert.Equal(60, production.Score);
        Assert.Contains(production.Reasons, r => r.Contains("AMC Banking 365 Fundamentals"));
        Assert.Contains(production.Reasons, r => r.Contains("in 30.0 (production is on 29.0)"));

        Assert.Equal(0, MatchScorer.Score(amc, Distribution).Score);
    }

    [Fact]
    public void Named_dependency_matches_the_deprecation_of_that_standard()
    {
        var peppol = Item("Peppol BIS 2.0 and Peppol BIS 2.1 are replaced by Peppol BIS 3.0", RoadmapChangeType.Deprecation, version: "30.0");

        Assert.True(MatchScorer.Score(peppol, Distribution).IsCandidate);
        Assert.False(MatchScorer.Score(peppol, Production).IsCandidate);
    }

    [Fact]
    public void A_name_needs_all_its_distinctive_words()
    {
        // Matched "Finance reports API (beta)" on two of three words in the first real run.
        Assert.False(MatchScorer.NameAppearsIn("Finance reports API (beta)", "Reporting and data analysis - Trace G/L account usage in finance reports"));
        Assert.True(MatchScorer.NameAppearsIn("Finance reports API (beta)", "Finance reports API (beta) (removal)"));
        Assert.True(MatchScorer.NameAppearsIn("Shopify Connector", "Ecommerce - Keep Shopify connections current"));
        Assert.False(MatchScorer.NameAppearsIn("Document Output", "Design document reports with the updated Word add-in"));
    }

    [Fact]
    public void Untriaged_module_tags_count_half_and_alone_are_not_a_candidate()
    {
        var guess = MatchScorer.Score(Item("Reporting - Bookmark list views", modules: ["Reporting"]), Production);
        Assert.Equal(MatchScorer.UnconfirmedModuleOverlap, guess.Score);
        Assert.False(guess.IsCandidate);

        var confirmed = MatchScorer.Score(Item("Reporting - Bookmark list views", modules: ["Reporting"], triaged: true), Production);
        Assert.Equal(MatchScorer.ModuleOverlap, confirmed.Score);
        Assert.True(confirmed.IsCandidate);
    }

    [Fact]
    public void Urgency_amplifies_a_match_but_never_creates_one()
    {
        Assert.Equal(0, MatchScorer.Score(Item("Something unrelated (removal)", RoadmapChangeType.Retirement), Production).Score);
    }

    [Fact]
    public void Soap_items_hit_customers_that_publish_SOAP()
    {
        var soap = Item("Remove feature key: SOAP publishing for standard pages", RoadmapChangeType.Retirement);

        var production = MatchScorer.Score(soap, Production);
        Assert.Contains(production.Reasons, r => r.Contains("SOAP endpoints (ProductionOrders (shop-floor MES)"));
        Assert.True(production.IsCandidate);
        Assert.Equal(0, MatchScorer.Score(soap, Distribution).Score);
    }

    [Fact]
    public void Copilot_features_count_only_for_customers_using_Copilot()
    {
        var copilot = Item("Copilot and agents - Manage agent permissions easier");

        Assert.Contains(MatchScorer.Score(copilot, Distribution).Reasons, r => r.Contains("Copilot"));
        Assert.Equal(0, MatchScorer.Score(copilot, Production).Score);
    }

    [Fact]
    public void Confirmed_object_overlap_scores_forty()
    {
        var item = Item("Production order changes", RoadmapChangeType.BehaviourChange);
        item.ObjectsTouched = ["Production Order"];

        var result = MatchScorer.Score(item, Production);
        Assert.Contains(result.Reasons, r => r.StartsWith("+40 custom extensions touch Production Order"));
    }

    [Fact]
    public void New_functionality_outside_the_customers_modules_is_penalised_never_below_zero()
    {
        var result = MatchScorer.Score(Item("Manufacturing - New capacity planning view", RoadmapChangeType.NewCapability, ["Manufacturing"]), Distribution);

        Assert.Equal(0, result.Score);
        Assert.Contains(result.Reasons, r => r.StartsWith("-20"));
    }
}

public sealed class MatchRunnerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BcReleasePlanDbContext> _options;
    private readonly Guid _amcId = Guid.NewGuid();
    private readonly Guid _sharePointId = Guid.NewGuid();

    public MatchRunnerTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options;
        using var db = Db();
        db.Database.Migrate();
        CustomerSeeder.SeedAsync(db, File.ReadAllText("Samples/customers.sample.json")).GetAwaiter().GetResult();
        db.RoadmapItems.AddRange(
            new RoadmapItem { Id = _amcId, ExternalId = "30.0/amc", Product = "bc", Title = "AMC Fundamentals app (removal)", ChangeType = RoadmapChangeType.Retirement, PayloadHash = "" },
            new RoadmapItem { Id = _sharePointId, ExternalId = "561025", Product = "power_automate_demo", Title = "SharePoint: Power Automate trigger for forms", Modules = ["Reporting"], PayloadHash = "" });
        db.SaveChanges();
    }

    private BcReleasePlanDbContext Db() => new(_options);

    [Fact]
    public async Task Stores_candidates_with_reasons_and_skips_other_products()
    {
        await using (var db = Db())
        {
            var result = await MatchRunner.RunAsync(db);
            Assert.Equal(1, result.Added);
        }

        await using var read = Db();
        var row = await read.CustomerItems.Include(ci => ci.Customer).SingleAsync();
        Assert.Equal(_amcId, row.RoadmapItemId);
        Assert.StartsWith("Brightline", row.Customer!.Name);
        Assert.Equal(CustomerItemRelevance.Unscreened, row.Relevance);
        Assert.NotEmpty(row.MatchReasons);
    }

    [Fact]
    public async Task Rerun_is_a_no_op()
    {
        await using (var db = Db()) await MatchRunner.RunAsync(db);
        await using (var db = Db())
        {
            Assert.Equal(new MatchRunner.RunResult(0, 0, 0, 1), await MatchRunner.RunAsync(db));
        }
    }

    [Fact]
    public async Task A_row_someone_worked_on_survives_dropping_below_the_threshold()
    {
        await using (var db = Db()) await MatchRunner.RunAsync(db);
        await using (var db = Db())
        {
            var row = await db.CustomerItems.SingleAsync();
            row.Decision = CustomerItemDecision.Adopt;
            var customer = await db.Customers.SingleAsync(c => c.Id == row.CustomerId);
            customer.AppSourceApps = []; // AMC uninstalled: no longer a match
            await db.SaveChangesAsync();
        }

        await using (var db = Db())
        {
            var result = await MatchRunner.RunAsync(db);
            Assert.Equal(0, result.Removed);
        }

        await using var read = Db();
        var kept = await read.CustomerItems.SingleAsync();
        Assert.Equal(CustomerItemDecision.Adopt, kept.Decision);
        Assert.Equal(0, kept.MatchScore);
    }

    [Fact]
    public async Task An_untouched_row_is_removed_when_it_stops_matching()
    {
        await using (var db = Db()) await MatchRunner.RunAsync(db);
        await using (var db = Db())
        {
            var customer = await db.Customers.SingleAsync(c => c.Name.StartsWith("Brightline"));
            customer.AppSourceApps = [];
            await db.SaveChangesAsync();
        }

        await using (var db = Db())
        {
            Assert.Equal(1, (await MatchRunner.RunAsync(db)).Removed);
        }
    }

    public void Dispose() => _connection.Dispose();
}
