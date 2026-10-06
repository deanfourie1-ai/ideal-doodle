using BcReleasePlanPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

/// <summary>
/// The writes behind the Triage screen (design doc §8 Screen 1): a person confirms the
/// classifiers' reading of an item, corrects it, or reopens a decision. Every write stamps or
/// clears <see cref="RoadmapItem.TriagedAt"/>, which is what tells ingest to keep the human values.
/// Uses a context per call because the screen is a long-lived interactive component.
/// </summary>
public sealed class RoadmapTriageService(IDbContextFactory<BcReleasePlanDbContext> contextFactory, TimeProvider timeProvider)
{
    public Task ConfirmAsync(Guid itemId, CancellationToken ct = default) =>
        UpdateAsync(itemId, item => Triage(item), ct);

    public Task CorrectAsync(Guid itemId, RoadmapChangeType changeType, IReadOnlyCollection<string> modules, CancellationToken ct = default)
    {
        var unknown = modules.Except(BcModules.All).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Not BC modules: {string.Join(", ", unknown)}", nameof(modules));
        }

        if (changeType == RoadmapChangeType.Unknown)
        {
            throw new ArgumentException("A triaged item needs a change type.", nameof(changeType));
        }

        return UpdateAsync(itemId, item =>
        {
            item.ChangeType = changeType;
            // Stored in taxonomy order so the same choice always serializes the same way.
            item.Modules = BcModules.All.Where(modules.Contains).ToList();
            Triage(item);
        }, ct);
    }

    /// <summary>Undo a decision: back into the queue, and ingest owns the fields again.</summary>
    public Task ReopenAsync(Guid itemId, CancellationToken ct = default) =>
        UpdateAsync(itemId, item =>
        {
            item.TriagedAt = null;
            item.NeedsConfirmation = true;
        }, ct);

    private void Triage(RoadmapItem item)
    {
        item.TriagedAt = timeProvider.GetUtcNow();
        item.NeedsConfirmation = false;
    }

    private async Task UpdateAsync(Guid itemId, Action<RoadmapItem> change, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var item = await db.RoadmapItems.SingleOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new InvalidOperationException($"Roadmap item {itemId} not found.");
        change(item);
        await db.SaveChangesAsync(ct);
    }
}
