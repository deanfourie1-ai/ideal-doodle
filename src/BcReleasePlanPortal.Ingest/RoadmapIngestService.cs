using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Abstractions;
using BcReleasePlanPortal.Ingest.Alerts;
using BcReleasePlanPortal.Ingest.Configuration;
using BcReleasePlanPortal.Ingest.Diffing;
using BcReleasePlanPortal.Ingest.Learn;
using BcReleasePlanPortal.Ingest.Mcp;
using BcReleasePlanPortal.Ingest.Normalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BcReleasePlanPortal.Ingest;

/// <summary>
/// Orchestrates one full ingest run (design doc §6): for each configured product, page through
/// the MCP roadmap list, hydrate only items that are new or that Microsoft has touched since
/// our last run, normalize, diff against the stored state, persist, and alert on anything
/// urgent. Intended to run once a day (design doc: "Full re-fetch, not incremental — the volume
/// is small and idempotency is worth more than efficiency") but every step here is also safe to
/// call more than once a day if needed.
/// </summary>
public sealed class RoadmapIngestService(
    IRoadmapMcpClient mcpClient,
    RoadmapItemNormalizer normalizer,
    LearnDeprecationNormalizer deprecationNormalizer,
    IRoadmapItemStore store,
    IIngestAlertSink alertSink,
    ILearnPageSource learnPageSource,
    IOptions<RoadmapIngestOptions> options,
    TimeProvider timeProvider,
    ILogger<RoadmapIngestService> logger)
{
    public async Task<IngestRunResult> RunAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var result = new IngestRunResult { StartedAt = now };
        var learnVersions = await ListLearnVersionsAsync(ct);
        var learnTargetVersions = await LoadLearnTargetVersionsAsync(learnVersions, ct);

        foreach (var filter in options.Value.ProductFilters)
        {
            var productResult = new ProductIngestResult { InternalProduct = filter.InternalProduct };
            result.Products.Add(productResult);

            try
            {
                await IngestProductAsync(filter, learnTargetVersions, now, productResult, ct);
            }
            catch (Exception ex)
            {
                // One product family failing (e.g. a bad filter, a transient MCP error) should
                // not stop the others from ingesting — design doc §13 favours degrading loudly
                // over an all-or-nothing daily job.
                logger.LogError(ex, "Ingest failed for product {Product}", filter.InternalProduct);
            }
        }

        var deprecationResult = new ProductIngestResult { InternalProduct = $"{options.Value.LearnInternalProduct} (Learn deprecations)" };
        result.Products.Add(deprecationResult);
        try
        {
            await IngestLearnDeprecationsAsync(learnVersions, now, deprecationResult, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingest failed for Learn deprecations");
        }

        result.FinishedAt = timeProvider.GetUtcNow();
        logger.LogInformation(
            "Ingest run complete: {Seen} seen, {New} new, {Updated} updated, {Events} change events, {Alerts} alerts sent",
            result.TotalItemsSeen, result.TotalItemsNew, result.TotalItemsUpdated, result.TotalChangeEvents, result.TotalAlertsSent);
        return result;
    }

    private async Task IngestProductAsync(ProductFilter filter, IReadOnlyDictionary<string, string>? learnTargetVersions, DateTimeOffset now, ProductIngestResult productResult, CancellationToken ct)
    {
        var skip = 0;
        while (true)
        {
            var page = await mcpClient.GetRecentRoadmapsAsync(filter.MicrosoftProductTags, skip, ct);
            productResult.ItemsSeen += page.Items.Count;

            foreach (var summary in page.Items)
            {
                var externalId = summary.Id.ToString();
                var existing = await store.FindAsync(RoadmapItemSource.Roadmap, externalId, ct);

                var isNew = existing is null;
                var targetVersion = LearnTargetVersions.Resolve(learnTargetVersions, externalId, existing?.TargetVersion);
                var isTouchedSinceLastSeen = existing is not null
                    && (existing.SourceModifiedAt != summary.Modified || existing.TargetVersion != targetVersion);

                if (!isNew && !isTouchedSinceLastSeen)
                {
                    // Nothing Microsoft-side changed since we last hydrated this item — just
                    // bump LastSeenAt so we can tell "still on the roadmap" from "vanished".
                    existing!.LastSeenAt = now;
                    await store.UpsertAsync(existing, ct);
                    continue;
                }

                var hydrated = await mcpClient.GetRoadmapByIdAsync(externalId, ct);
                var current = normalizer.Normalize(hydrated, filter.InternalProduct, now);
                current.TargetVersion = targetVersion;
                current.PayloadHash = PayloadHasher.Compute(current);

                await PersistAsync(existing, current, now, productResult, ct);
            }

            if (!page.HasMore)
            {
                break;
            }

            skip += page.ReturnedCount;
        }
    }

    /// <summary>
    /// Deprecated-features sections from the current BC major version onward, each as its own
    /// item. Earlier waves' removals have already happened to every online tenant, so they're
    /// history, not plan input. No Learn version list means no "current" to compare against, so
    /// the pass is skipped rather than guessing.
    /// </summary>
    private async Task IngestLearnDeprecationsAsync(IReadOnlyList<string>? learnVersions, DateTimeOffset now, ProductIngestResult productResult, CancellationToken ct)
    {
        var currentMajor = learnVersions is null ? null : LearnTargetVersions.MostRecentMajors(learnVersions, 1).FirstOrDefault();
        if (currentMajor is null)
        {
            logger.LogWarning("Learn deprecations skipped: current BC version unknown");
            return;
        }

        var page = await learnPageSource.FetchDeprecatedFeaturesAsync(ct);
        if (!page.Available)
        {
            return;
        }

        var currentMajorNumber = LearnTargetVersions.MajorOf(currentMajor);
        foreach (var deprecation in page.Items.Where(d => LearnTargetVersions.MajorOf(d.Version) >= currentMajorNumber))
        {
            productResult.ItemsSeen++;
            var current = deprecationNormalizer.Normalize(deprecation, options.Value.LearnInternalProduct, currentMajorNumber, now);
            var existing = await store.FindAsync(RoadmapItemSource.LearnDeprecation, current.ExternalId, ct);
            await PersistAsync(existing, current, now, productResult, ct);
        }
    }

    /// <summary>
    /// Shared tail of both sources: insert a new item, or diff a changed one into ChangeEvents and
    /// alert on anything urgent (design doc §6 steps 5–6).
    /// </summary>
    private async Task PersistAsync(RoadmapItem? existing, RoadmapItem current, DateTimeOffset now, ProductIngestResult productResult, CancellationToken ct)
    {
        if (existing is null)
        {
            await store.UpsertAsync(current, ct);
            productResult.ItemsNew++;
            return;
        }

        current.Id = existing.Id;
        current.FirstSeenAt = existing.FirstSeenAt;
        if (TriageCarryOver.Apply(existing, current))
        {
            logger.LogWarning("Triaged item {ExternalId} reclassified as {ChangeType}; sent back to triage", current.ExternalId, current.ChangeType);
        }

        if (current.PayloadHash == existing.PayloadHash)
        {
            // Microsoft touched metadata we don't track (e.g. internal re-save) —
            // nothing worth diffing, just refresh our bookkeeping.
            existing.LastSeenAt = now;
            existing.SourceModifiedAt = current.SourceModifiedAt;
            await store.UpsertAsync(existing, ct);
            return;
        }

        var events = ChangeEventDetector.Detect(existing, current, now);
        foreach (var changeEvent in events)
        {
            changeEvent.RoadmapItemId = current.Id;
        }

        await store.UpsertAsync(current, ct);
        await store.AddChangeEventsAsync(events, ct);
        productResult.ItemsUpdated++;
        productResult.ChangeEventsEmitted += events.Count;

        var alreadyPublished = await store.IsReferencedByAnyReleasePlanAsync(current.Id, ct);
        if (ChangeEventDetector.RequiresImmediateAlert(current, events, alreadyPublished))
        {
            await alertSink.SendAsync(current, events, ct);
            productResult.AlertsSent++;
        }
    }

    /// <summary>Every update version Learn lists, or null when Learn can't be reached.</summary>
    private async Task<IReadOnlyList<string>?> ListLearnVersionsAsync(CancellationToken ct)
    {
        try
        {
            var versions = await learnPageSource.ListWhatsNewVersionsAsync(ct);
            return versions.Count == 0 ? null : versions;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Learn version list unavailable");
            return null;
        }
    }

    /// <summary>
    /// Roadmap ID → BC update version, from the "What's new" pages of the most recent major updates.
    /// Null when Learn can't be read at all, so callers keep each item's known version rather than
    /// wiping it — a Learn outage must never look like Microsoft un-scheduling 80 features.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>?> LoadLearnTargetVersionsAsync(IReadOnlyList<string>? learnVersions, CancellationToken ct)
    {
        if (learnVersions is null)
        {
            return null;
        }

        try
        {
            var majors = LearnTargetVersions.MostRecentMajors(learnVersions, options.Value.LearnMajorVersionsToRead);

            var pages = new List<(string Version, LearnPageResult Page)>();
            foreach (var version in majors)
            {
                pages.Add((version, await learnPageSource.FetchWhatsNewAsync(version, ct)));
            }

            if (!pages.Any(p => p.Page.Available))
            {
                logger.LogWarning("No Learn 'what's new' page could be read; target versions left as they were");
                return null;
            }

            var map = LearnTargetVersions.Build(pages);
            logger.LogInformation("Learn: {Count} roadmap IDs mapped to updates {Versions}", map.Count, string.Join(", ", majors));
            return map;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Learn target versions unavailable; target versions left as they were");
            return null;
        }
    }
}
