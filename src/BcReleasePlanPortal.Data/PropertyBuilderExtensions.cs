using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BcReleasePlanPortal.Data;

internal static class PropertyBuilderExtensions
{
    /// <summary>Maps a collection/object property to a JSON text column — see <see cref="JsonValueConverter{T}"/>.</summary>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder)
    {
        builder.HasConversion(new JsonValueConverter<T>());
        builder.Metadata.SetValueComparer(new JsonValueComparer<T>());
        return builder;
    }
}
