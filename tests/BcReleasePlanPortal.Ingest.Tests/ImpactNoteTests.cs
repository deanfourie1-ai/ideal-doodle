using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public sealed class ImpactNoteTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BcReleasePlanDbContext> _options;
    private readonly ImpactNoteService _notes;
    private readonly Guid _amcId = Guid.NewGuid();
    private readonly Guid _unmatchedId = Guid.NewGuid();

    private static readonly ImpactNoteService.Draft AmcNote = new(
        "Microsoft removes its AMC Fundamentals app in BC 30.0; AMC ships its own replacement.",
        "Bank statement import and payment exports stop working without the replacement.",
        "Agree the migration date with AMC before the 30.0 update.",
        EffortBand.S,
        RiskLevel.High);

    public ImpactNoteTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options;
        using var db = new BcReleasePlanDbContext(_options);
        db.Database.Migrate();
        CustomerSeeder.SeedAsync(db, File.ReadAllText("Samples/customers.sample.json")).GetAwaiter().GetResult();
        db.RoadmapItems.AddRange(
            new RoadmapItem { Id = _amcId, ExternalId = "30.0/amc", Product = "bc", Title = "AMC Fundamentals app (removal)", ChangeType = RoadmapChangeType.Retirement, TargetVersion = "30.0", PayloadHash = "" },
            new RoadmapItem { Id = _unmatchedId, ExternalId = "1", Product = "bc", Title = "Something nobody uses", PayloadHash = "" });
        db.SaveChanges();
        MatchRunner.RunAsync(db).GetAwaiter().GetResult();
        _notes = new ImpactNoteService(new Factory(_options), TimeProvider.System);
    }

    [Fact]
    public async Task Work_list_holds_only_items_on_a_customers_board()
    {
        var work = Assert.Single(await _notes.ListAsync());

        Assert.Equal(_amcId, work.Item.Id);
        Assert.Equal(ImpactNoteStatus.None, work.Status);
        Assert.Equal(1, work.CustomersAffected);
    }

    [Fact]
    public async Task Editor_shows_the_affected_customers()
    {
        var editor = await _notes.LoadAsync(_amcId);

        var customer = Assert.Single(editor!.Customers);
        Assert.StartsWith("Brightline", customer.Name);
        Assert.True(customer.IsSample);
        Assert.Null(editor.Note);
    }

    [Fact]
    public async Task A_draft_is_saved_trimmed_and_stays_unreviewed()
    {
        await _notes.SaveDraftAsync(_amcId, AmcNote with { Summary = "  " + AmcNote.Summary + "  " });

        var editor = await _notes.LoadAsync(_amcId);
        Assert.Equal(AmcNote.Summary, editor!.Note!.Summary);
        Assert.Equal(EffortBand.S, editor.Note.EffortBand);
        Assert.Null(editor.Note.ReviewedAt);
        Assert.Equal(ImpactNoteStatus.Draft, editor.Status);
    }

    [Fact]
    public async Task Marking_reviewed_needs_a_summary()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _notes.MarkReviewedAsync(_amcId, AmcNote with { Summary = " " }));
        Assert.Null((await _notes.LoadAsync(_amcId))!.Note);
    }

    [Fact]
    public async Task Editing_a_reviewed_note_makes_it_a_draft_again()
    {
        await _notes.MarkReviewedAsync(_amcId, AmcNote);
        Assert.Equal(ImpactNoteStatus.Reviewed, (await _notes.LoadAsync(_amcId))!.Status);

        await _notes.SaveDraftAsync(_amcId, AmcNote with { ActionRequired = "Changed my mind." });

        Assert.Equal(ImpactNoteStatus.Draft, (await _notes.LoadAsync(_amcId))!.Status);
    }

    [Fact]
    public async Task A_Microsoft_change_after_review_flags_the_note()
    {
        await _notes.MarkReviewedAsync(_amcId, AmcNote);

        await using (var db = new BcReleasePlanDbContext(_options))
        {
            db.ChangeEvents.Add(new ChangeEvent
            {
                Id = Guid.NewGuid(),
                RoadmapItemId = _amcId,
                Field = nameof(RoadmapItem.TargetVersion),
                OldValue = "30.0",
                NewValue = "31.0",
                DetectedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var editor = await _notes.LoadAsync(_amcId);
        Assert.Equal(ImpactNoteStatus.ChangedSinceReview, editor!.Status);
        Assert.Equal("31.0", Assert.Single(editor.ChangesSinceReview).NewValue);
        Assert.Equal(ImpactNoteStatus.ChangedSinceReview, Assert.Single(await _notes.ListAsync()).Status);

        await _notes.MarkReviewedAsync(_amcId, AmcNote);
        Assert.Equal(ImpactNoteStatus.Reviewed, (await _notes.LoadAsync(_amcId))!.Status);
    }

    [Fact]
    public async Task One_note_per_item_however_often_it_is_saved()
    {
        await _notes.SaveDraftAsync(_amcId, AmcNote);
        await _notes.MarkReviewedAsync(_amcId, AmcNote);

        await using var db = new BcReleasePlanDbContext(_options);
        Assert.Equal(1, await db.ImpactNotes.CountAsync());
    }

    public void Dispose() => _connection.Dispose();

    private sealed class Factory(DbContextOptions<BcReleasePlanDbContext> options) : IDbContextFactory<BcReleasePlanDbContext>
    {
        public BcReleasePlanDbContext CreateDbContext() => new(options);
    }
}
