using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Matching;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

public enum ImpactNoteStatus
{
    None,
    Draft,
    Reviewed,

    /// <summary>Reviewed, but Microsoft changed the item afterwards — the note may no longer be true.</summary>
    ChangedSinceReview,
}

/// <summary>
/// Reads and writes behind the impact note screens (design doc §5.3, §8): one explanation per
/// roadmap item, written once and reused for every customer it affects. A note is a draft until
/// someone marks it reviewed; publishing (Phase 4) is meant to use reviewed notes only.
/// </summary>
public sealed class ImpactNoteService(IDbContextFactory<BcReleasePlanDbContext> contextFactory, TimeProvider timeProvider)
{
    public sealed record AffectedCustomer(Guid CustomerId, string Name, bool IsSample, int MatchScore, CustomerItemDecision Decision, string OverrideNote);

    public sealed record WorkItem(RoadmapItem Item, ImpactNoteStatus Status, int CustomersAffected);

    public sealed record Editor(RoadmapItem Item, ImpactNote? Note, ImpactNoteStatus Status, IReadOnlyList<ChangeEvent> ChangesSinceReview, IReadOnlyList<AffectedCustomer> Customers);

    public sealed record Draft(string Summary, string WhyItMatters, string ActionRequired, EffortBand EffortBand, RiskLevel Risk);

    /// <summary>
    /// Every item that matters to at least one customer, with its note status — the queue of notes
    /// to write. Urgent items and items affecting more customers come first.
    /// </summary>
    public async Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var affected = (await db.CustomerItems.AsNoTracking().ToListAsync(ct))
            .Where(IsOnABoard)
            .GroupBy(ci => ci.RoadmapItemId)
            .ToDictionary(g => g.Key, g => g.Count());
        var ids = affected.Keys.ToList();

        var items = await db.RoadmapItems.AsNoTracking().Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        var notes = await db.ImpactNotes.AsNoTracking().Where(n => ids.Contains(n.RoadmapItemId)).ToDictionaryAsync(n => n.RoadmapItemId, ct);
        var events = await db.ChangeEvents.AsNoTracking().Where(e => ids.Contains(e.RoadmapItemId)).ToListAsync(ct);

        return items
            .Select(item => new WorkItem(item, StatusOf(notes.GetValueOrDefault(item.Id), events.Where(e => e.RoadmapItemId == item.Id)), affected[item.Id]))
            .OrderByDescending(w => IsUrgent(w.Item.ChangeType))
            .ThenByDescending(w => w.CustomersAffected)
            .ThenBy(w => w.Item.Title)
            .ToList();
    }

    public async Task<Editor?> LoadAsync(Guid roadmapItemId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var item = await db.RoadmapItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == roadmapItemId, ct);
        if (item is null)
        {
            return null;
        }

        var note = await db.ImpactNotes.AsNoTracking().SingleOrDefaultAsync(n => n.RoadmapItemId == roadmapItemId, ct);
        var events = await db.ChangeEvents.AsNoTracking().Where(e => e.RoadmapItemId == roadmapItemId).ToListAsync(ct);
        var customers = (await db.CustomerItems.AsNoTracking().Include(ci => ci.Customer).Where(ci => ci.RoadmapItemId == roadmapItemId).ToListAsync(ct))
            .Where(IsOnABoard)
            .OrderByDescending(ci => ci.MatchScore)
            .Select(ci => new AffectedCustomer(ci.CustomerId, ci.Customer!.Name, ci.Customer.Flags.IsSample, ci.MatchScore, ci.Decision, ci.OverrideNote))
            .ToList();

        var changes = note?.ReviewedAt is { } reviewedAt
            ? events.Where(e => e.DetectedAt > reviewedAt).OrderBy(e => e.DetectedAt).ToList()
            : [];

        return new Editor(item, note, StatusOf(note, events), changes, customers);
    }

    public Task SaveDraftAsync(Guid roadmapItemId, Draft draft, CancellationToken ct = default) =>
        SaveAsync(roadmapItemId, draft, reviewed: false, ct);

    /// <summary>Saves and marks the note reviewed. A reviewed note needs at least a summary.</summary>
    public Task MarkReviewedAsync(Guid roadmapItemId, Draft draft, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(draft.Summary))
        {
            throw new ArgumentException("Write a summary before marking the note reviewed.", nameof(draft));
        }

        return SaveAsync(roadmapItemId, draft, reviewed: true, ct);
    }

    private async Task SaveAsync(Guid roadmapItemId, Draft draft, bool reviewed, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        if (!await db.RoadmapItems.AnyAsync(i => i.Id == roadmapItemId, ct))
        {
            throw new InvalidOperationException($"Roadmap item {roadmapItemId} not found.");
        }

        var note = await db.ImpactNotes.SingleOrDefaultAsync(n => n.RoadmapItemId == roadmapItemId, ct);
        if (note is null)
        {
            note = new ImpactNote { Id = Guid.NewGuid(), RoadmapItemId = roadmapItemId };
            db.ImpactNotes.Add(note);
        }

        note.Summary = draft.Summary.Trim();
        note.WhyItMatters = draft.WhyItMatters.Trim();
        note.ActionRequired = draft.ActionRequired.Trim();
        note.EffortBand = draft.EffortBand;
        note.Risk = draft.Risk;
        // Editing a reviewed note makes it a draft again until someone re-reviews it.
        note.ReviewedAt = reviewed ? timeProvider.GetUtcNow() : null;

        await db.SaveChangesAsync(ct);
    }

    private static ImpactNoteStatus StatusOf(ImpactNote? note, IEnumerable<ChangeEvent> events) => note switch
    {
        null => ImpactNoteStatus.None,
        { ReviewedAt: null } => ImpactNoteStatus.Draft,
        { ReviewedAt: { } reviewedAt } when events.Any(e => e.DetectedAt > reviewedAt) => ImpactNoteStatus.ChangedSinceReview,
        _ => ImpactNoteStatus.Reviewed,
    };

    /// <summary>Same rule as the decision board: a current candidate, or something already decided.</summary>
    private static bool IsOnABoard(CustomerItem ci) =>
        ci.MatchScore >= MatchScorer.CandidateThreshold || ci.Decision != CustomerItemDecision.Undecided;

    private static bool IsUrgent(RoadmapChangeType t) =>
        t is RoadmapChangeType.BreakingChange or RoadmapChangeType.Deprecation or RoadmapChangeType.Retirement;
}
