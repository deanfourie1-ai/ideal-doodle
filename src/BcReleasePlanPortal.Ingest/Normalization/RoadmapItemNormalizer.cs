using System.Globalization;
using BcReleasePlanPortal.Domain;
using BcReleasePlanPortal.Ingest.Diffing;
using BcReleasePlanPortal.Ingest.Mcp.Models;

namespace BcReleasePlanPortal.Ingest.Normalization;

/// <summary>
/// Maps a hydrated <see cref="McpRoadmapItem"/> onto the canonical <see cref="RoadmapItem"/>
/// schema (design doc §5.1, §6 step 4).
///
/// A few RoadmapItem fields simply have no source on the M365 roadmap MCP surface today:
/// <see cref="RoadmapItem.TargetVersion"/>, <see cref="RoadmapItem.ObjectsTouched"/> and
/// <see cref="RoadmapItem.EnabledBy"/> are BC/Dynamics-specific concepts the doc expects to
/// come from the Learn "what's new"/deprecated-features scrapers (§6 step 3), which are a
/// separate, not-yet-reachable source (see <c>Learn.ILearnPageSource</c>). They're left at
/// their empty/Unknown defaults here rather than guessed.
/// </summary>
public sealed class RoadmapItemNormalizer(IChangeClassifier changeClassifier, IModuleClassifier moduleClassifier)
{
    public RoadmapItem Normalize(McpRoadmapItem source, string internalProduct, DateTimeOffset now)
    {
        var changeClassification = changeClassifier.Classify(source.Title, source.Description);
        var moduleClassification = moduleClassifier.Classify(source.Title, source.Description, source.Products);

        var item = new RoadmapItem
        {
            Id = Guid.NewGuid(),
            Source = RoadmapItemSource.Roadmap,
            ExternalId = source.Id.ToString(CultureInfo.InvariantCulture),
            Product = internalProduct,
            Title = source.Title,
            DescriptionRaw = source.Description,
            Url = source.MoreInfoUrls.FirstOrDefault(),
            Modules = [.. moduleClassification.Modules],
            ChangeType = changeClassification.ChangeType,
            NeedsConfirmation = !changeClassification.Confident || !moduleClassification.Confident,
            TargetVersion = null,
            PreviewDate = ParseYearMonth(source.PreviewAvailabilityDate),
            GaDate = ParseYearMonth(source.GeneralAvailabilityDate),
            Status = MapStatus(source.Status),
            EnabledBy = RoadmapEnabledBy.Unknown,
            ObjectsTouched = [],
            PayloadHash = string.Empty,
            SourceModifiedAt = source.Modified,
            FirstSeenAt = now,
            LastSeenAt = now,
        };

        item.PayloadHash = PayloadHasher.Compute(item);
        return item;
    }

    private static DateOnly? ParseYearMonth(string? yearMonth)
    {
        if (string.IsNullOrWhiteSpace(yearMonth))
        {
            return null;
        }

        return DateOnly.TryParseExact(yearMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? new DateOnly(parsed.Year, parsed.Month, 1)
            : null;
    }

    private static RoadmapItemStatus MapStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "in development" => RoadmapItemStatus.Planned,
        "rolling out" => RoadmapItemStatus.Ga,
        "launched" => RoadmapItemStatus.Ga,
        "cancelled" or "canceled" => RoadmapItemStatus.Cancelled,
        _ => RoadmapItemStatus.Unknown,
    };
}
