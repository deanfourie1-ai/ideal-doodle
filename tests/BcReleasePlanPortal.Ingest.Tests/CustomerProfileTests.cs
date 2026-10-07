using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public sealed class CustomerProfileTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BcReleasePlanDbContext> _options;
    private readonly CustomerProfileService _profiles;
    private readonly CustomerBoardService _boards;

    public CustomerProfileTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options;
        using var db = new BcReleasePlanDbContext(_options);
        db.Database.Migrate();
        db.RoadmapItems.Add(new RoadmapItem { Id = Guid.NewGuid(), ExternalId = "30.0/amc", Product = "bc", Title = "AMC Fundamentals app (removal)", ChangeType = RoadmapChangeType.Retirement, PayloadHash = "" });
        db.SaveChanges();
        var factory = new Factory(_options);
        _profiles = new CustomerProfileService(factory);
        _boards = new CustomerBoardService(factory, TimeProvider.System);
    }

    private static Customer Profile(string name = "Van Dijk Metaal B.V.") => new()
    {
        Name = name,
        Environments = [new CustomerEnvironment { Name = "Production", Version = "29.0", Country = "NL" }, new CustomerEnvironment { Name = "  " }],
        ModulesInUse = ["Finance", "Manufacturing"],
        AppSourceApps = [new CustomerAppSourceApp { Publisher = "AMC Consult A/S", Name = "AMC Banking 365 Fundamentals" }, new CustomerAppSourceApp()],
        CustomExtensions = [new CustomerExtension { Name = "Shop floor", ExtendsObjects = ["Production Order", " ", "Production Order"] }],
        Integrations = new CustomerIntegrations { Soap = true, SoapEndpoints = ["MES ", ""], Other = ["Peppol BIS 3.0", " "] },
        Flags = new CustomerFlags { IsSample = true },
    };

    [Fact]
    public async Task A_new_customer_is_real_cleaned_up_and_matched_straight_away()
    {
        var id = await _profiles.CreateAsync(Profile());

        var saved = (await _profiles.LoadAsync(id))!;
        Assert.False(saved.Flags.IsSample);
        Assert.Single(saved.Environments);
        Assert.Single(saved.AppSourceApps);
        Assert.Equal(["Production Order"], saved.CustomExtensions[0].ExtendsObjects);
        Assert.Equal(["MES"], saved.Integrations.SoapEndpoints);
        Assert.Equal(["Peppol BIS 3.0"], saved.Integrations.Other);

        var card = Assert.Single((await _boards.LoadAsync(id))!.Items);
        Assert.Contains(card.MatchReasons, r => r.Contains("AMC Banking 365 Fundamentals"));
    }

    [Fact]
    public async Task Names_must_be_present_and_unique()
    {
        await _profiles.CreateAsync(Profile());

        await Assert.ThrowsAsync<ArgumentException>(() => _profiles.CreateAsync(Profile("van dijk metaal b.v.")));
        await Assert.ThrowsAsync<ArgumentException>(() => _profiles.CreateAsync(Profile("  ")));
    }

    [Fact]
    public async Task Editing_rescores_and_keeps_decisions()
    {
        var id = await _profiles.CreateAsync(Profile());
        var card = Assert.Single((await _boards.LoadAsync(id))!.Items);
        await _boards.DecideAsync(card.Id, CustomerItemDecision.TestFirst);

        var edited = Profile();
        edited.AppSourceApps = [];
        await _profiles.UpdateAsync(id, edited);

        var kept = Assert.Single((await _boards.LoadAsync(id))!.Items);
        Assert.Equal(CustomerItemDecision.TestFirst, kept.Decision);
        Assert.Equal(0, kept.MatchScore);
    }

    [Fact]
    public async Task Deleting_removes_the_customer_and_its_board()
    {
        var id = await _profiles.CreateAsync(Profile());

        await _profiles.DeleteAsync(id);

        await using var db = new BcReleasePlanDbContext(_options);
        Assert.False(await db.Customers.AnyAsync());
        Assert.False(await db.CustomerItems.AnyAsync());
    }

    [Fact]
    public async Task A_customer_with_a_published_plan_cannot_be_deleted()
    {
        var id = await _profiles.CreateAsync(Profile());
        await using (var db = new BcReleasePlanDbContext(_options))
        {
            db.ReleasePlans.Add(new ReleasePlan { Id = Guid.NewGuid(), CustomerId = id, Version = "1.0", PeriodLabel = "October 2026 – March 2027", Status = ReleasePlanStatus.Published });
            await db.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _profiles.DeleteAsync(id));
        Assert.Contains("published release plan", error.Message);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class Factory(DbContextOptions<BcReleasePlanDbContext> options) : IDbContextFactory<BcReleasePlanDbContext>
    {
        public BcReleasePlanDbContext CreateDbContext() => new(options);
    }
}
