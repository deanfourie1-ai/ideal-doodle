using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Ingest;
using BcReleasePlanPortal.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBcReleasePlanData(builder.Configuration);
builder.Services.AddRoadmapIngest(builder.Configuration);
builder.Services.AddSingleton<DailyIngestBackgroundService>();

var runOnce = args.Contains("--run-once");

if (!runOnce)
{
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DailyIngestBackgroundService>());
}

var host = builder.Build();

// Applies pending EF Core migrations on startup — fine for a single-instance worker with a
// local SQLite file; revisit if this ever runs as more than one instance.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BcReleasePlanDbContext>();
    await db.Database.MigrateAsync();
}

// --seed-customers <file>: load hand-maintained customer profiles (e.g. samples/customers.sample.json), then exit.
var seedIndex = Array.IndexOf(args, "--seed-customers");
if (seedIndex >= 0)
{
    if (seedIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("--seed-customers needs a JSON file path.");
        Environment.ExitCode = 1;
        return;
    }

    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BcReleasePlanDbContext>();
    var seeded = await CustomerSeeder.SeedAsync(db, await File.ReadAllTextAsync(args[seedIndex + 1]));
    Console.WriteLine($"Customers: {seeded.Added} added, {seeded.Updated} updated.");
    return;
}

if (runOnce)
{
    var ingestRunner = host.Services.GetRequiredService<DailyIngestBackgroundService>();
    await ingestRunner.RunOnceAsync(CancellationToken.None);
    return;
}

await host.RunAsync();
