using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Matching;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

/// <summary>
/// Reads and writes behind the per-customer decision board (design doc §8): what a consultant
/// has decided about each matched roadmap item for one customer. Decisions live on
/// <see cref="CustomerItem"/>, which the match engine never overwrites.
/// </summary>
public sealed class CustomerBoardService(IDbContextFactory<BcReleasePlanDbContext> contextFactory, TimeProvider timeProvider)
{
    public sealed record Board(Customer Customer, IReadOnlyList<CustomerItem> Items);

    /// <summary>
    /// The customer's candidates plus anything already decided — a decision stays on the board
    /// even if its item later stops matching, so nobody loses track of what was agreed.
    /// </summary>
    public async Task<Board?> LoadAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
        {
            return null;
        }

        var items = (await db.CustomerItems.AsNoTracking()
                .Include(ci => ci.RoadmapItem)
                .Where(ci => ci.CustomerId == customerId)
                .ToListAsync(ct))
            .Where(ci => ci.MatchScore >= MatchScorer.CandidateThreshold || ci.Decision != CustomerItemDecision.Undecided)
            .OrderByDescending(ci => ci.MatchScore)
            .ThenBy(ci => ci.RoadmapItem!.Title)
            .ToList();

        return new Board(customer, items);
    }

    public Task DecideAsync(Guid customerItemId, CustomerItemDecision decision, CancellationToken ct = default) =>
        UpdateAsync(customerItemId, row =>
        {
            row.Decision = decision;
            row.DecidedAt = decision == CustomerItemDecision.Undecided ? null : timeProvider.GetUtcNow();
        }, ct);

    /// <summary>The customer-specific addendum to the shared impact note (design doc §5.5).</summary>
    public Task SetNoteAsync(Guid customerItemId, string note, CancellationToken ct = default) =>
        UpdateAsync(customerItemId, row => row.OverrideNote = note.Trim(), ct);

    private async Task UpdateAsync(Guid customerItemId, Action<CustomerItem> change, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var row = await db.CustomerItems.SingleOrDefaultAsync(ci => ci.Id == customerItemId, ct)
            ?? throw new InvalidOperationException($"Customer item {customerItemId} not found.");
        change(row);
        await db.SaveChangesAsync(ct);
    }
}
