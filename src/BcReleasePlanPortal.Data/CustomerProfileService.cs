using BcReleasePlanPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

/// <summary>
/// Adds, edits and deletes customer profiles from the portal — the hand-maintained profile entry
/// design doc §5.4 expects for the MVP. Every save re-runs matching, so the customer's board and
/// the Customers page reflect the new profile immediately.
/// </summary>
public sealed class CustomerProfileService(IDbContextFactory<BcReleasePlanDbContext> contextFactory)
{
    public async Task<Customer?> LoadAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
    }

    /// <summary>Creates the customer and returns its id. New customers are always real: <see cref="CustomerFlags.IsSample"/> is forced off.</summary>
    public async Task<Guid> CreateAsync(Customer profile, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        await ValidateAsync(db, profile, existingId: null, ct);

        profile.Id = Guid.NewGuid();
        profile.Flags.IsSample = false;
        profile.Items = [];
        db.Customers.Add(profile);
        await db.SaveChangesAsync(ct);
        await MatchRunner.RunAsync(db, ct);
        return profile.Id;
    }

    /// <summary>Replaces the profile fields. Decisions, notes and release plans are untouched; the sample flag is kept as it was.</summary>
    public async Task UpdateAsync(Guid customerId, Customer profile, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var existing = await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new InvalidOperationException("Customer not found.");
        await ValidateAsync(db, profile, customerId, ct);

        existing.Name = profile.Name;
        existing.TenantId = profile.TenantId;
        existing.Environments = profile.Environments;
        existing.ModulesInUse = profile.ModulesInUse;
        existing.AppSourceApps = profile.AppSourceApps;
        existing.CustomExtensions = profile.CustomExtensions;
        existing.Integrations = profile.Integrations;
        existing.Flags = new CustomerFlags
        {
            UsesCopilot = profile.Flags.UsesCopilot,
            MultiCompany = profile.Flags.MultiCompany,
            HasTestEnvironment = profile.Flags.HasTestEnvironment,
            IsSample = existing.Flags.IsSample,
        };
        existing.Contacts = profile.Contacts;
        existing.ReviewCadence = profile.ReviewCadence;

        await db.SaveChangesAsync(ct);
        await MatchRunner.RunAsync(db, ct);
    }

    /// <summary>
    /// Deletes the customer with its matches and board decisions. Refused once a release plan has
    /// been published for them: that plan is the record of what the customer was sent.
    /// </summary>
    public async Task DeleteAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new InvalidOperationException("Customer not found.");
        if (await db.ReleasePlans.AnyAsync(p => p.CustomerId == customerId && p.Status != ReleasePlanStatus.Draft, ct))
        {
            throw new InvalidOperationException($"{customer.Name} has a published release plan, so the profile is kept as the record of what was sent.");
        }

        db.CustomerItems.RemoveRange(db.CustomerItems.Where(ci => ci.CustomerId == customerId));
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ValidateAsync(BcReleasePlanDbContext db, Customer profile, Guid? existingId, CancellationToken ct)
    {
        profile.Name = profile.Name.Trim();
        if (profile.Name.Length == 0)
        {
            throw new ArgumentException("Give the customer a name.");
        }

        var names = await db.Customers.Where(c => c.Id != existingId).Select(c => c.Name).ToListAsync(ct);
        if (names.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"There is already a customer called {profile.Name}.");
        }

        var unknown = profile.ModulesInUse.Except(BcModules.All).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Not BC modules: {string.Join(", ", unknown)}");
        }

        // Drop half-filled rows rather than storing blanks the match engine would trip over.
        profile.Environments = profile.Environments.Where(e => !string.IsNullOrWhiteSpace(e.Name)).ToList();
        profile.AppSourceApps = profile.AppSourceApps.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        profile.CustomExtensions = profile.CustomExtensions.Where(e => !string.IsNullOrWhiteSpace(e.Name)).ToList();
        profile.Contacts = profile.Contacts.Where(c => !string.IsNullOrWhiteSpace(c.Role) || !string.IsNullOrWhiteSpace(c.Name)).ToList();
        profile.Integrations.SoapEndpoints = Clean(profile.Integrations.SoapEndpoints);
        profile.Integrations.Other = Clean(profile.Integrations.Other);
        foreach (var extension in profile.CustomExtensions)
        {
            extension.ExtendsObjects = Clean(extension.ExtendsObjects);
        }
    }

    private static List<string> Clean(IEnumerable<string> values) =>
        values.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
