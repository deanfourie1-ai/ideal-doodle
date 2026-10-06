using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public sealed class CustomerBoardTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BcReleasePlanDbContext> _options;
    private readonly CustomerBoardService _boards;
    private readonly Guid _brightlineId;

    public CustomerBoardTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options;
        using var db = new BcReleasePlanDbContext(_options);
        db.Database.Migrate();
        CustomerSeeder.SeedAsync(db, File.ReadAllText("Samples/customers.sample.json")).GetAwaiter().GetResult();
        db.RoadmapItems.Add(new RoadmapItem { Id = Guid.NewGuid(), ExternalId = "30.0/amc", Product = "bc", Title = "AMC Fundamentals app (removal)", ChangeType = RoadmapChangeType.Retirement, PayloadHash = "" });
        db.SaveChanges();
        MatchRunner.RunAsync(db).GetAwaiter().GetResult();
        _brightlineId = db.Customers.Single(c => c.Name.StartsWith("Brightline")).Id;
        _boards = new CustomerBoardService(new Factory(_options), TimeProvider.System);
    }

    private async Task<CustomerItem> OnlyCard() => Assert.Single((await _boards.LoadAsync(_brightlineId))!.Items);

    [Fact]
    public async Task Board_shows_the_customers_candidates_undecided()
    {
        var card = await OnlyCard();

        Assert.Equal(CustomerItemDecision.Undecided, card.Decision);
        Assert.Equal("AMC Fundamentals app (removal)", card.RoadmapItem!.Title);
    }

    [Fact]
    public async Task Deciding_stamps_the_time_and_moving_back_clears_it()
    {
        var card = await OnlyCard();

        await _boards.DecideAsync(card.Id, CustomerItemDecision.Blocked);
        var decided = await OnlyCard();
        Assert.Equal(CustomerItemDecision.Blocked, decided.Decision);
        Assert.NotNull(decided.DecidedAt);

        await _boards.DecideAsync(card.Id, CustomerItemDecision.Undecided);
        Assert.Null((await OnlyCard()).DecidedAt);
    }

    [Fact]
    public async Task Note_is_saved_trimmed()
    {
        var card = await OnlyCard();

        await _boards.SetNoteAsync(card.Id, "  Agree migration date with AMC.  ");

        Assert.Equal("Agree migration date with AMC.", (await OnlyCard()).OverrideNote);
    }

    [Fact]
    public async Task A_decision_stays_on_the_board_after_the_match_drops_away()
    {
        var card = await OnlyCard();
        await _boards.DecideAsync(card.Id, CustomerItemDecision.Adopt);

        await using (var db = new BcReleasePlanDbContext(_options))
        {
            var brightline = await db.Customers.SingleAsync(c => c.Id == _brightlineId);
            brightline.AppSourceApps = [];
            await db.SaveChangesAsync();
            await MatchRunner.RunAsync(db);
        }

        var kept = await OnlyCard();
        Assert.Equal(CustomerItemDecision.Adopt, kept.Decision);
        Assert.Equal(0, kept.MatchScore);
    }

    [Fact]
    public async Task Unknown_customer_returns_null()
    {
        Assert.Null(await _boards.LoadAsync(Guid.NewGuid()));
    }

    public void Dispose() => _connection.Dispose();

    private sealed class Factory(DbContextOptions<BcReleasePlanDbContext> options) : IDbContextFactory<BcReleasePlanDbContext>
    {
        public BcReleasePlanDbContext CreateDbContext() => new(options);
    }
}
