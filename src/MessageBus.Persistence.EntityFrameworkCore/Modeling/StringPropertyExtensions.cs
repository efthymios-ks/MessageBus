using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MessageBus.Persistence.EntityFrameworkCore.Modeling;

internal static class StringPropertyExtensions
{
    /// <summary>
    /// Unbounded when no length is configured — maps to <c>nvarchar(max)</c> and its equivalents.
    /// A nullable length has no <see cref="PropertyBuilder{TProperty}.HasMaxLength"/> overload,
    /// and silently defaulting to a number would cap payloads at a size nobody chose.
    /// </summary>
    public static PropertyBuilder<string> WithMaxLength(this PropertyBuilder<string> property, int? maxLength)
        => maxLength is { } length ? property.HasMaxLength(length) : property;
}
