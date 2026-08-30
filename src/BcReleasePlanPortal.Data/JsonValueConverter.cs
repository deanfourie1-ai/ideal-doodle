using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BcReleasePlanPortal.Data;

/// <summary>
/// SQLite has no native array/object column type, so every List&lt;T&gt; or nested object
/// property on an entity (RoadmapItem.Modules, Customer.Environments, etc.) is stored as a JSON
/// text column via this converter. Fine for the MVP's read-the-whole-row access pattern (design
/// doc §5.4 already expects CustomerProfile fields to be maintained and read as a document);
/// revisit if a future phase needs to query inside these collections.
/// </summary>
public sealed class JsonValueConverter<T>() : ValueConverter<T, string>(
    value => JsonSerializer.Serialize(value, JsonOptions),
    json => JsonSerializer.Deserialize<T>(json, JsonOptions)!)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

/// <summary>Structural (value-based) change tracking for JSON-backed collection/object properties.</summary>
public sealed class JsonValueComparer<T>() : ValueComparer<T>(
    (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
    v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
