using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BcReleasePlanPortal.Ingest.Tests;

public sealed class CustomerSeederTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BcReleasePlanDbContext> _options;
    private static readonly string SampleJson = File.ReadAllText("Samples/customers.sample.json");

    public CustomerSeederTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BcReleasePlanDbContext>().UseSqlite(_connection).Options;
        using var db = new BcReleasePlanDbContext(_options);
        db.Database.Migrate();
    }

    [Fact]
    public async Task Loads_the_two_sample_customers_flagged_as_samples()
    {
        await using (var db = new BcReleasePlanDbContext(_options))
        {
            Assert.Equal(new CustomerSeeder.SeedResult(2, 0), await CustomerSeeder.SeedAsync(db, SampleJson));
        }

        await using var read = new BcReleasePlanDbContext(_options);
        var customers = await read.Customers.OrderBy(c => c.Name).ToListAsync();
        Assert.All(customers, c => Assert.True(c.Flags.IsSample));
        Assert.All(customers, c => Assert.EndsWith("(sample)", c.Name));

        var production = customers[0];
        Assert.Contains("Manufacturing", production.ModulesInUse);
        Assert.Contains(production.AppSourceApps, a => a.Name.Contains("AMC Banking"));
        Assert.True(production.Integrations.Soap);

        var distribution = customers[1];
        Assert.DoesNotContain("Manufacturing", distribution.ModulesInUse);
        Assert.Contains("Peppol BIS 2.1", distribution.Integrations.Other);
        Assert.Contains("Finance reports API (beta)", distribution.Integrations.Other);
    }

    [Fact]
    public async Task Rerunning_updates_in_place_instead_of_duplicating()
    {
        await using (var db = new BcReleasePlanDbContext(_options))
        {
            await CustomerSeeder.SeedAsync(db, SampleJson);
        }

        await using (var db = new BcReleasePlanDbContext(_options))
        {
            Assert.Equal(new CustomerSeeder.SeedResult(0, 2), await CustomerSeeder.SeedAsync(db, SampleJson));
        }

        await using var read = new BcReleasePlanDbContext(_options);
        Assert.Equal(2, await read.Customers.CountAsync());
    }

    [Fact]
    public async Task Rejects_a_module_outside_the_taxonomy()
    {
        await using var db = new BcReleasePlanDbContext(_options);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CustomerSeeder.SeedAsync(db, """[{ "Name": "X", "ModulesInUse": ["Ecommerce"] }]"""));
    }

    public void Dispose() => _connection.Dispose();
}
