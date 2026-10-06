using System.Text.Json;
using BcReleasePlanPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

/// <summary>
/// Loads customer profiles from a JSON file (an array of <see cref="Customer"/>) — the hand-maintained
/// profile entry design doc §5.4 expects for the MVP, until Phase 7 automates it. Matches existing
/// customers by name and replaces their profile fields, so re-running after editing the file updates
/// in place rather than duplicating. Never touches a customer's <see cref="Customer.Items"/>.
/// </summary>
public static class CustomerSeeder
{
    public sealed record SeedResult(int Added, int Updated);

    public static async Task<SeedResult> SeedAsync(BcReleasePlanDbContext db, string json, CancellationToken ct = default)
    {
        var profiles = JsonSerializer.Deserialize<List<Customer>>(json, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })
            ?? throw new InvalidDataException("Customer file is empty.");

        var unknownModules = profiles.SelectMany(p => p.ModulesInUse).Except(BcModules.All).Distinct().ToList();
        if (unknownModules.Count > 0)
        {
            throw new InvalidDataException($"Not BC modules: {string.Join(", ", unknownModules)}");
        }

        var added = 0;
        var updated = 0;
        foreach (var profile in profiles)
        {
            var existing = await db.Customers.SingleOrDefaultAsync(c => c.Name == profile.Name, ct);
            if (existing is null)
            {
                profile.Id = Guid.NewGuid();
                db.Customers.Add(profile);
                added++;
                continue;
            }

            existing.TenantId = profile.TenantId;
            existing.Environments = profile.Environments;
            existing.ModulesInUse = profile.ModulesInUse;
            existing.AppSourceApps = profile.AppSourceApps;
            existing.CustomExtensions = profile.CustomExtensions;
            existing.Integrations = profile.Integrations;
            existing.Flags = profile.Flags;
            existing.Contacts = profile.Contacts;
            existing.ReviewCadence = profile.ReviewCadence;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return new SeedResult(added, updated);
    }
}
