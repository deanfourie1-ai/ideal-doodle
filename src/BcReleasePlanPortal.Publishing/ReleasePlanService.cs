using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BcReleasePlanPortal.Publishing;

public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    /// <summary>Where published .docx files are kept, one folder per customer. Relative paths resolve against the working directory.</summary>
    public string OutputFolder { get; set; } = "release-plans";
}

/// <summary>
/// Preview and publish a customer's release plan (design doc §9). A preview renders the current
/// state and changes nothing. Publishing freezes the plan — every line's text copied into
/// <see cref="ReleasePlanLine"/> — under the next version number, supersedes the previous version,
/// and keeps the .docx, so what a customer was sent never changes afterwards.
/// </summary>
public sealed class ReleasePlanService(
    IDbContextFactory<BcReleasePlanDbContext> contextFactory,
    TimeProvider timeProvider,
    IOptions<PublishingOptions> options)
{
    public sealed record Readiness(
        Customer Customer,
        IReadOnlyList<PlanLine> Lines,
        IReadOnlyList<PlanLine> MissingNotes,
        IReadOnlyList<ReleasePlan> Published,
        string NextVersion)
    {
        /// <summary>Why publishing is refused right now; empty when it can go ahead.</summary>
        public IReadOnlyList<string> Blockers =>
        [
            .. Customer.Flags.IsSample ? ["This is a sample customer. Sample profiles can be previewed but never published."] : Array.Empty<string>(),
            .. Lines.Count == 0 ? ["Nothing is in the plan yet: decide items on the customer's board first."] : Array.Empty<string>(),
            .. MissingNotes.Count > 0 ? [$"{MissingNotes.Count} {(MissingNotes.Count == 1 ? "item needs" : "items need")} a reviewed impact note."] : Array.Empty<string>(),
        ];
    }

    public sealed record DocumentFile(byte[] Content, string FileName);

    public async Task<Readiness?> GetReadinessAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
        {
            return null;
        }

        var lines = await CurrentLinesAsync(db, customerId, ct);
        var published = (await db.ReleasePlans.AsNoTracking().Where(p => p.CustomerId == customerId).ToListAsync(ct))
            .OrderByDescending(p => p.PublishedAt)
            .ToList();

        return new Readiness(customer, lines, lines.Where(l => !l.HasReviewedNote).ToList(), published, PlanRules.NextVersion(published.FirstOrDefault()?.Version));
    }

    public async Task<DocumentFile?> PreviewAsync(Guid customerId, string periodLabel, CancellationToken ct = default)
    {
        var readiness = await GetReadinessAsync(customerId, ct);
        if (readiness is null)
        {
            return null;
        }

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var previous = readiness.Published.FirstOrDefault();
        var plan = new PlanDocument(
            readiness.Customer.Name,
            readiness.Customer.Flags.IsSample,
            IsPreview: true,
            readiness.NextVersion,
            periodLabel,
            Today(),
            readiness.Lines,
            previous is null ? null : PlanRules.Compare(previous.Version, await FrozenLinesAsync(db, previous.Id, ct), readiness.Lines));

        return new DocumentFile(WordPlanWriter.Write(plan), FileName(readiness.Customer.Name, $"{readiness.NextVersion}-preview"));
    }

    /// <summary>Freezes and publishes the plan. Throws <see cref="InvalidOperationException"/> listing the blockers if it can't.</summary>
    public async Task<ReleasePlan> PublishAsync(Guid customerId, string periodLabel, CancellationToken ct = default)
    {
        var readiness = await GetReadinessAsync(customerId, ct)
            ?? throw new InvalidOperationException("Customer not found.");
        if (readiness.Blockers.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", readiness.Blockers));
        }

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var previous = readiness.Published.FirstOrDefault(p => p.Status == ReleasePlanStatus.Published);
        var plan = new ReleasePlan
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Version = readiness.NextVersion,
            PeriodLabel = periodLabel.Trim(),
            PublishedAt = timeProvider.GetUtcNow(),
            Status = ReleasePlanStatus.Published,
            Lines = readiness.Lines.Select((l, i) => new ReleasePlanLine
            {
                Id = Guid.NewGuid(),
                RoadmapItemId = l.RoadmapItemId,
                Title = l.Title,
                Summary = l.Summary,
                WhyItMatters = l.WhyItMatters,
                Action = l.Action,
                CustomerNote = l.CustomerNote,
                ChangeType = l.ChangeType,
                TargetVersion = l.TargetVersion,
                GaDate = l.GaDate,
                EffortBand = l.EffortBand,
                Risk = l.Risk,
                Decision = l.Decision,
                SortOrder = i,
            }).ToList(),
        };

        var document = new PlanDocument(
            readiness.Customer.Name, IsSample: false, IsPreview: false, plan.Version, plan.PeriodLabel, Today(), readiness.Lines,
            previous is null ? null : PlanRules.Compare(previous.Version, await FrozenLinesAsync(db, previous.Id, ct), readiness.Lines));

        var folder = Path.GetFullPath(Path.Combine(options.Value.OutputFolder, Slug(readiness.Customer.Name)));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName(readiness.Customer.Name, plan.Version));
        await File.WriteAllBytesAsync(path, WordPlanWriter.Write(document), ct);
        plan.DocumentPath = path;

        if (previous is not null)
        {
            var tracked = await db.ReleasePlans.SingleAsync(p => p.Id == previous.Id, ct);
            tracked.Status = ReleasePlanStatus.Superseded;
        }

        db.ReleasePlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan;
    }

    /// <summary>The file exactly as published.</summary>
    public async Task<DocumentFile?> DownloadAsync(Guid planId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var plan = await db.ReleasePlans.AsNoTracking().Include(p => p.Customer).SingleOrDefaultAsync(p => p.Id == planId, ct);
        if (plan?.DocumentPath is null || !File.Exists(plan.DocumentPath))
        {
            return null;
        }

        return new DocumentFile(await File.ReadAllBytesAsync(plan.DocumentPath, ct), Path.GetFileName(plan.DocumentPath));
    }

    private static async Task<IReadOnlyList<PlanLine>> CurrentLinesAsync(BcReleasePlanDbContext db, Guid customerId, CancellationToken ct)
    {
        var rows = await db.CustomerItems.AsNoTracking().Include(ci => ci.RoadmapItem).Where(ci => ci.CustomerId == customerId).ToListAsync(ct);
        var itemIds = rows.Select(r => r.RoadmapItemId).ToList();
        var notes = await db.ImpactNotes.AsNoTracking().Where(n => itemIds.Contains(n.RoadmapItemId)).ToDictionaryAsync(n => n.RoadmapItemId, ct);
        var events = await db.ChangeEvents.AsNoTracking().Where(e => itemIds.Contains(e.RoadmapItemId)).ToListAsync(ct);

        var lines = new List<PlanLine>();
        foreach (var row in rows)
        {
            var item = row.RoadmapItem!;
            if (PlanRules.SectionFor(item.ChangeType, row.Decision) is not { } section)
            {
                continue;
            }

            var note = notes.GetValueOrDefault(item.Id);
            // Reviewed, and Microsoft hasn't changed the item since — the same test as the
            // Impact notes page's "Changed since review".
            var reviewed = note?.ReviewedAt is { } at && !events.Any(e => e.RoadmapItemId == item.Id && e.DetectedAt > at);

            lines.Add(new PlanLine(
                item.Id, DisplayTitle(item.Title), section, item.ChangeType, item.TargetVersion, item.GaDate,
                note?.EffortBand ?? EffortBand.None, note?.Risk ?? RiskLevel.Low, row.Decision,
                note?.Summary ?? "", note?.WhyItMatters ?? "", note?.ActionRequired ?? "", row.OverrideNote, reviewed));
        }

        return PlanRules.Order(lines);
    }

    private static async Task<IReadOnlyList<PlanLine>> FrozenLinesAsync(BcReleasePlanDbContext db, Guid planId, CancellationToken ct) =>
        (await db.ReleasePlanLines.AsNoTracking().Where(l => l.ReleasePlanId == planId).OrderBy(l => l.SortOrder).ToListAsync(ct))
            .Select(l => new PlanLine(
                l.RoadmapItemId, l.Title, PlanRules.SectionFor(l.ChangeType, l.Decision) ?? PlanSection.ForInformation,
                l.ChangeType, l.TargetVersion, l.GaDate, l.EffortBand, l.Risk, l.Decision,
                l.Summary, l.WhyItMatters, l.Action, l.CustomerNote, HasReviewedNote: true))
            .ToList();

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static string DisplayTitle(string title) =>
        title.StartsWith("Dynamics 365 Business Central: ", StringComparison.Ordinal) ? title["Dynamics 365 Business Central: ".Length..] : title;

    public static string FileName(string customerName, string version) => $"Release plan - {Slug(customerName)} - v{version}.docx";

    private static string Slug(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
}
