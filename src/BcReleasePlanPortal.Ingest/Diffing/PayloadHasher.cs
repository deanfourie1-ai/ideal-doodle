using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BcReleasePlanPortal.Domain;

namespace BcReleasePlanPortal.Ingest.Diffing;

/// <summary>
/// Computes RoadmapItem.PayloadHash: a hash of only the fields that matter for delta detection
/// (design doc §5.1, §6 step 5) — i.e. everything Microsoft could change on re-publish, but not
/// our own bookkeeping (Id, FirstSeenAt/LastSeenAt, NeedsConfirmation is derived from the same
/// inputs so it's excluded to avoid double-signalling a change that's really just a re-run of
/// the classifier).
/// </summary>
public static class PayloadHasher
{
    public static string Compute(RoadmapItem item)
    {
        var canonical = new
        {
            item.Title,
            item.DescriptionRaw,
            item.Status,
            item.ChangeType,
            item.TargetVersion,
            item.PreviewDate,
            item.GaDate,
            item.EnabledBy,
            Modules = item.Modules.OrderBy(m => m, StringComparer.Ordinal),
            ObjectsTouched = item.ObjectsTouched.OrderBy(o => o, StringComparer.Ordinal),
        };

        var json = JsonSerializer.Serialize(canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
