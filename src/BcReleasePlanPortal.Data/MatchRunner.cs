using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Matching;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

/// <summary>
/// Re-scores every roadmap item against every customer and syncs <see cref="CustomerItem"/> rows
/// (design doc §7). Cheap enough to run in full after each ingest and each profile load — items ×
/// customers is small. Only writes the engine's own fields (score, reasons): relevance, decision,
/// notes and owner belong to people, and a row someone has worked on is never deleted, even if
/// its score drops below the threshold — they need to see that it dropped.
/// </summary>
public static class MatchRunner
{
    public sealed record RunResult(int Added, int Updated, int Removed, int Candidates);

    /// <param name="product">
    /// Only items of this internal product are scored. Customer profiles describe BC tenants (BC
    /// modules, AL extensions, BC versions), so a SharePoint or Microsoft 365 item has nothing to be
    /// compared against — a profile shape per product family comes with ingesting one for real.
    /// </param>
    public static async Task<RunResult> RunAsync(BcReleasePlanDbContext db, CancellationToken ct = default, string product = "bc")
    {
        var items = await db.RoadmapItems.AsNoTracking().Where(i => i.Product == product).ToListAsync(ct);
        var customers = await db.Customers.AsNoTracking().ToListAsync(ct);
        var itemIds = items.Select(i => i.Id).ToHashSet();
        var allRows = await db.CustomerItems.ToListAsync(ct);
        var existing = allRows
            .Where(ci => itemIds.Contains(ci.RoadmapItemId))
            .ToDictionary(ci => (ci.CustomerId, ci.RoadmapItemId));

        int added = 0, updated = 0, removed = 0, candidates = 0;

        // Rows for items outside the scored product can't be re-scored, so they'd go stale; drop
        // the ones nobody has worked on.
        foreach (var stale in allRows.Where(ci => !itemIds.Contains(ci.RoadmapItemId) && IsUntouched(ci)))
        {
            db.CustomerItems.Remove(stale);
            removed++;
        }

        foreach (var customer in customers)
        {
            foreach (var item in items)
            {
                var match = MatchScorer.Score(item, customer);
                existing.TryGetValue((customer.Id, item.Id), out var row);

                if (match.IsCandidate)
                {
                    candidates++;
                }

                if (row is null)
                {
                    if (match.IsCandidate)
                    {
                        db.CustomerItems.Add(new CustomerItem
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = customer.Id,
                            RoadmapItemId = item.Id,
                            MatchScore = match.Score,
                            MatchReasons = [.. match.Reasons],
                        });
                        added++;
                    }

                    continue;
                }

                if (!match.IsCandidate && IsUntouched(row))
                {
                    db.CustomerItems.Remove(row);
                    removed++;
                    continue;
                }

                if (row.MatchScore != match.Score || !row.MatchReasons.SequenceEqual(match.Reasons))
                {
                    row.MatchScore = match.Score;
                    row.MatchReasons = [.. match.Reasons];
                    updated++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new RunResult(added, updated, removed, candidates);
    }

    private static bool IsUntouched(CustomerItem row) =>
        row.Relevance == CustomerItemRelevance.Unscreened
        && row.Decision == CustomerItemDecision.Undecided
        && string.IsNullOrEmpty(row.OverrideNote)
        && row.Owner is null
        && row.DecidedAt is null;
}
