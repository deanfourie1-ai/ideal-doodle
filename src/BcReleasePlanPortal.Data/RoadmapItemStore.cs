using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BcReleasePlanPortal.Data;

public sealed class RoadmapItemStore(BcReleasePlanDbContext db) : IRoadmapItemStore
{
    public Task<RoadmapItem?> FindAsync(RoadmapItemSource source, string externalId, CancellationToken ct) =>
        db.RoadmapItems.FirstOrDefaultAsync(x => x.Source == source && x.ExternalId == externalId, ct);

    public async Task UpsertAsync(RoadmapItem item, CancellationToken ct)
    {
        var tracked = await db.RoadmapItems.FindAsync([item.Id], ct);
        if (tracked is null)
        {
            db.RoadmapItems.Add(item);
        }
        else if (!ReferenceEquals(tracked, item))
        {
            db.Entry(tracked).CurrentValues.SetValues(item);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task AddChangeEventsAsync(IEnumerable<ChangeEvent> events, CancellationToken ct)
    {
        foreach (var changeEvent in events)
        {
            changeEvent.Id = changeEvent.Id == Guid.Empty ? Guid.NewGuid() : changeEvent.Id;
        }

        db.ChangeEvents.AddRange(events);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsReferencedByAnyReleasePlanAsync(Guid roadmapItemId, CancellationToken ct) =>
        db.ReleasePlanLines.AnyAsync(x => x.RoadmapItemId == roadmapItemId, ct);
}
