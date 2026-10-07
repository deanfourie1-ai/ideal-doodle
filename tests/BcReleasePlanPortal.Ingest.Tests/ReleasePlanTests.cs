using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Publishing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public class PlanRulesTests
{
    [Theory]
    [InlineData(RoadmapChangeType.Retirement, CustomerItemDecision.Undecided, PlanSection.Mandatory)]
    [InlineData(RoadmapChangeType.Deprecation, CustomerItemDecision.TestFirst, PlanSection.Mandatory)]
    [InlineData(RoadmapChangeType.Enhancement, CustomerItemDecision.Adopt, PlanSection.Recommended)]
    [InlineData(RoadmapChangeType.NewCapability, CustomerItemDecision.TestFirst, PlanSection.Recommended)]
    [InlineData(RoadmapChangeType.Enhancement, CustomerItemDecision.Blocked, PlanSection.ForInformation)]
    public void Puts_items_in_the_right_section(RoadmapChangeType type, CustomerItemDecision decision, PlanSection expected)
    {
        Assert.Equal(expected, PlanRules.SectionFor(type, decision));
    }

    [Theory]
    [InlineData(RoadmapChangeType.Retirement, CustomerItemDecision.Ignore)]
    [InlineData(RoadmapChangeType.Enhancement, CustomerItemDecision.Undecided)]
    public void Leaves_out_ignored_and_undiscussed_items(RoadmapChangeType type, CustomerItemDecision decision)
    {
        Assert.Null(PlanRules.SectionFor(type, decision));
    }

    [Theory]
    [InlineData(null, "1.0")]
    [InlineData("1.0", "1.1")]
    [InlineData("1.9", "1.10")]
    public void Numbers_versions(string? latest, string expected) => Assert.Equal(expected, PlanRules.NextVersion(latest));

    [Fact]
    public void Default_period_is_one_release_wave() =>
        Assert.Equal("October 2026 – March 2027", PlanRules.DefaultPeriod(new DateOnly(2026, 10, 7)));
}

public sealed class ReleasePlanServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BcReleasePlanDbContext> _options;
    private readonly string _outputFolder = Path.Combine(Path.GetTempPath(), "bcrpp-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ReleasePlanService _plans;
    private readonly ImpactNoteService _notes;
    private readonly CustomerBoardService _boards;
    private readonly Guid _realCustomer;
    private readonly Guid _sampleCustomer;
    private readonly Guid _amcId = Guid.NewGuid();

    public ReleasePlanServiceTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options;
        using (var db = new BcReleasePlanDbContext(_options))
        {
            db.Database.Migrate();
            CustomerSeeder.SeedAsync(db, File.ReadAllText("Samples/customers.sample.json")).GetAwaiter().GetResult();
            // A non-sample copy of the production profile, so the publish path can be exercised.
            var sample = db.Customers.AsNoTracking().Single(c => c.Name.StartsWith("Brightline"));
            sample.Id = Guid.NewGuid();
            sample.Name = "Real Fabrication B.V.";
            sample.Flags = new CustomerFlags { IsSample = false, HasTestEnvironment = true };
            db.Customers.Add(sample);
            db.RoadmapItems.Add(new RoadmapItem
            {
                Id = _amcId, ExternalId = "30.0/amc", Product = "bc", Title = "AMC Fundamentals app (removal)",
                ChangeType = RoadmapChangeType.Retirement, TargetVersion = "30.0", GaDate = new DateOnly(2027, 4, 1), PayloadHash = "",
            });
            db.SaveChanges();
            MatchRunner.RunAsync(db).GetAwaiter().GetResult();
            _realCustomer = sample.Id;
            _sampleCustomer = db.Customers.Single(c => c.Name.StartsWith("Brightline")).Id;
        }

        var factory = new Factory(_options);
        _plans = new ReleasePlanService(factory, TimeProvider.System, Options.Create(new PublishingOptions { OutputFolder = _outputFolder }));
        _notes = new ImpactNoteService(factory, TimeProvider.System);
        _boards = new CustomerBoardService(factory, TimeProvider.System);
    }

    private static readonly ImpactNoteService.Draft AmcNote = new(
        "Microsoft removes its AMC Fundamentals app in BC 30.0.",
        "Bank statement import stops without AMC's replacement app.",
        "Agree the migration date with AMC before the 30.0 update.",
        EffortBand.S, RiskLevel.High);

    [Fact]
    public async Task An_undecided_removal_is_already_in_the_plan_as_mandatory()
    {
        var readiness = await _plans.GetReadinessAsync(_realCustomer);

        var line = Assert.Single(readiness!.Lines);
        Assert.Equal(PlanSection.Mandatory, line.Section);
        Assert.Equal("1.0", readiness.NextVersion);
    }

    [Fact]
    public async Task Publishing_is_refused_without_a_reviewed_note()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _plans.PublishAsync(_realCustomer, "October 2026 – March 2027"));
        Assert.Contains("reviewed impact note", error.Message);

        await _notes.SaveDraftAsync(_amcId, AmcNote);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _plans.PublishAsync(_realCustomer, "October 2026 – March 2027"));
    }

    [Fact]
    public async Task Publishing_a_sample_customer_is_refused_even_when_ready()
    {
        await _notes.MarkReviewedAsync(_amcId, AmcNote);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _plans.PublishAsync(_sampleCustomer, "October 2026 – March 2027"));
        Assert.Contains("sample customer", error.Message);
    }

    [Fact]
    public async Task Publishing_freezes_the_text_and_numbers_the_versions()
    {
        await _notes.MarkReviewedAsync(_amcId, AmcNote);
        var first = await _plans.PublishAsync(_realCustomer, "October 2026 – March 2027");
        Assert.Equal("1.0", first.Version);
        Assert.True(File.Exists(first.DocumentPath));

        // Change the live note: the published version must not move.
        await _notes.MarkReviewedAsync(_amcId, AmcNote with { Summary = "Rewritten later." });
        var second = await _plans.PublishAsync(_realCustomer, "October 2026 – March 2027");
        Assert.Equal("1.1", second.Version);

        await using var db = new BcReleasePlanDbContext(_options);
        var plans = await db.ReleasePlans.Include(p => p.Lines).ToListAsync();
        var v1 = plans.Single(p => p.Version == "1.0");
        Assert.Equal(ReleasePlanStatus.Superseded, v1.Status);
        Assert.Equal(AmcNote.Summary, Assert.Single(v1.Lines).Summary);
        Assert.Equal("Rewritten later.", Assert.Single(plans.Single(p => p.Version == "1.1").Lines).Summary);
    }

    [Fact]
    public async Task Preview_is_a_valid_word_document_with_the_plan_in_it()
    {
        await _notes.MarkReviewedAsync(_amcId, AmcNote);
        var card = Assert.Single((await _boards.LoadAsync(_realCustomer))!.Items);
        await _boards.SetNoteAsync(card.Id, "Their bank file goes to Rabobank every morning.");

        var preview = await _plans.PreviewAsync(_realCustomer, "October 2026 – March 2027");

        var text = AssertValidWordAndGetText(preview!.Content);
        Assert.Contains("Release plan", text);
        Assert.Contains("Real Fabrication B.V.", text);
        Assert.Contains("PREVIEW", text);
        Assert.Contains("Mandatory changes", text);
        Assert.Contains("AMC Fundamentals app (removal)", text);
        Assert.Contains("Agree the migration date with AMC", text);
        Assert.Contains("Their bank file goes to Rabobank every morning.", text);
        Assert.Contains("This is the first version of this plan.", text);
        Assert.DoesNotContain("SAMPLE CUSTOMER", text);
    }

    [Fact]
    public async Task Sample_preview_is_marked_not_for_distribution()
    {
        var preview = await _plans.PreviewAsync(_sampleCustomer, "October 2026 – March 2027");

        var text = AssertValidWordAndGetText(preview!.Content);
        Assert.Contains("SAMPLE CUSTOMER — NOT FOR DISTRIBUTION", text);
        Assert.Contains("No reviewed impact note yet", text);
    }

    [Fact]
    public async Task Second_version_lists_what_changed()
    {
        await _notes.MarkReviewedAsync(_amcId, AmcNote);
        await _plans.PublishAsync(_realCustomer, "October 2026 – March 2027");
        var card = Assert.Single((await _boards.LoadAsync(_realCustomer))!.Items);
        await _boards.DecideAsync(card.Id, CustomerItemDecision.TestFirst);

        var v2 = await _plans.PublishAsync(_realCustomer, "October 2026 – March 2027");
        var text = AssertValidWordAndGetText((await _plans.DownloadAsync(v2.Id))!.Content);

        Assert.Contains("Compared with version 1.0", text);
        Assert.Contains("Decision changed from To be agreed to Test first: AMC Fundamentals app (removal)", text);
    }

    private static string AssertValidWordAndGetText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return doc.MainDocumentPart!.Document.Body!.InnerText;
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_outputFolder)) Directory.Delete(_outputFolder, recursive: true);
    }

    private sealed class Factory(DbContextOptions<BcReleasePlanDbContext> options) : IDbContextFactory<BcReleasePlanDbContext>
    {
        public BcReleasePlanDbContext CreateDbContext() => new(options);
    }
}
