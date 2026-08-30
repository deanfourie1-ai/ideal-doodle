using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Domain.Abstractions;
using BcReleasePlanPortal.Ingest.Alerts;
using BcReleasePlanPortal.Ingest.Configuration;
using BcReleasePlanPortal.Ingest.Diffing;
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
    IRoadmapItemStore store,
    IIngestAlertSink alertSink,
    IOptions<RoadmapIngestOptions> options,
    TimeProvider timeProvider,
    ILogger<RoadmapIngestService> logger)
{
    public async Task<IngestRunResult> RunAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var result = new IngestRunResult { StartedAt = now };

        foreach (var filter in options.Value.ProductFilters)
        {
            var productResult = new ProductIngestResult { InternalProduct = filter.InternalProduct };
            result.Products.Add(productResult);

            try
            {
                await IngestProductAsync(filter, now, productResult, ct);
            }
            catch (Exception ex)
            {
                // One product family failing (e.g. a bad filter, a transient MCP error) should
                // not stop the others from ingesting — design doc §13 favours degrading loudly
                // over an all-or-nothing daily job.
                logger.LogError(ex, "Ingest failed for product {Product}", filter.InternalProduct);
            }
        }

        result.FinishedAt = timeProvider.GetUtcNow();
        logger.LogInformation(
            "Ingest run complete: {Seen} seen, {New} new, {Updated} updated, {Events} change events, {Alerts} alerts sent",
            result.TotalItemsSeen, result.TotalItemsNew, result.TotalItemsUpdated, result.TotalChangeEvents, result.TotalAlertsSent);
        return result;
    }

    private async Task IngestProductAsync(ProductFilter filter, DateTimeOffset now, ProductIngestResult productResult, CancellationToken ct)
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
                var isTouchedSinceLastSeen = existing is not null && existing.SourceModifiedAt != summary.Modified;

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

                if (isNew)
                {
                    await store.UpsertAsync(current, ct);
                    productResult.ItemsNew++;
                    continue;
                }

                current.Id = existing!.Id;
                current.FirstSeenAt = existing.FirstSeenAt;

                if (current.PayloadHash == existing.PayloadHash)
                {
                    // Microsoft touched metadata we don't track (e.g. internal re-save) —
                    // nothing worth diffing, just refresh our bookkeeping.
                    existing.LastSeenAt = now;
                    existing.SourceModifiedAt = current.SourceModifiedAt;
                    await store.UpsertAsync(existing, ct);
                    continue;
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

            if (!page.HasMore)
            {
                break;
            }

            skip += page.ReturnedCount;
        }
    }
}
