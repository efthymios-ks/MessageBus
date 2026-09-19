namespace MessageBus.Core.TypeResolution;

/// <summary>
/// The stable name of a CLR type used for saga discriminators and full-name message wire names.
/// Namespace plus class name, never the assembly-qualified form — the assembly-qualified string
/// carries a version, and a version change silently breaks every lookup.
/// </summary>
internal static class TypeNaming
{
    /// <summary>
    /// Returns <see cref="Type.FullName"/> and throws when it is missing.
    /// Named types always have one; open generic parameters, arrays without element context and
    /// anonymous types do not — none of which are valid sagas or message contracts.
    /// </summary>
    public static string NameOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.FullName
            ?? throw new InvalidOperationException($"'{type.Name}' has no full name to store under.");
    }
}
