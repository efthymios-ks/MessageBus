using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MessageBus.Explorer;

/// <summary>
/// Builds a plausible starting payload for a message type by reflection. Plausible, not valid: it
/// fills every property with something of the right shape so the JSON parses and the fields are
/// visible, and leaves what the values should actually be to whoever is about to send it.
/// </summary>
internal static class MessageSampleFactory
{
    /// <summary>
    /// Deep enough for a message worth sending, shallow enough that a self-referencing contract
    /// cannot spin. Recursion is also cut by type, so a cycle stops at its second appearance.
    /// </summary>
    private const int MaxDepth = 5;

    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    public static string Create(Type messageType, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return SampleFor(messageType, timeProvider, depth: 0, [])?.ToJsonString(_indented) ?? "{}";
    }

    private static JsonNode? SampleFor(Type type, TimeProvider timeProvider, int depth, HashSet<Type> seen)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        // Before the scalar check: Type.GetTypeCode reports an enum as its underlying integer, so
        // asking about scalars first would write a number the string-enum serializer cannot read.
        if (underlying.IsEnum)
        {
            var names = Enum.GetNames(underlying);

            return names.Length > 0 ? JsonValue.Create(names[0]) : null;
        }

        if (ScalarFor(underlying, timeProvider) is { } scalar)
        {
            return scalar;
        }

        if (ElementTypeOf(underlying) is { } elementType)
        {
            // One element rather than none: an empty array shows the property exists and hides
            // everything about what goes in it.
            var element = depth < MaxDepth ? SampleFor(elementType, timeProvider, depth + 1, seen) : null;

            return element is null ? [] : new JsonArray(element);
        }

        if (depth >= MaxDepth || !seen.Add(underlying))
        {
            return null;
        }

        try
        {
            return ObjectFor(underlying, timeProvider, depth, seen);
        }
        finally
        {
            // Removed on the way out, so a type used twice side by side is still expanded twice —
            // only an actual cycle is cut.
            seen.Remove(underlying);
        }
    }

    private static JsonObject ObjectFor(Type type, TimeProvider timeProvider, int depth, HashSet<Type> seen)
    {
        var sample = new JsonObject();

        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);

        foreach (var property in properties)
        {
            sample[CamelCase(property.Name)] = SampleFor(property.PropertyType, timeProvider, depth + 1, seen);
        }

        return sample;
    }

    private static JsonNode? ScalarFor(Type type, TimeProvider timeProvider)
        => Type.GetTypeCode(type) switch
        {
            TypeCode.String => JsonValue.Create(string.Empty),
            TypeCode.Boolean => JsonValue.Create(false),
            TypeCode.Char => JsonValue.Create(string.Empty),
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => JsonValue.Create(0),
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => JsonValue.Create(0),
            TypeCode.DateTime => JsonValue.Create(timeProvider.GetUtcNow().UtcDateTime),
            _ => OtherScalarFor(type, timeProvider)
        };

    private static JsonNode? OtherScalarFor(Type type, TimeProvider timeProvider)
    {
        if (type == typeof(Guid))
        {
            return JsonValue.Create(Guid.NewGuid());
        }

        if (type == typeof(DateTimeOffset))
        {
            return JsonValue.Create(timeProvider.GetUtcNow());
        }

        if (type == typeof(DateOnly))
        {
            return JsonValue.Create(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        }

        if (type == typeof(TimeOnly))
        {
            return JsonValue.Create(TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        }

        if (type == typeof(TimeSpan))
        {
            return JsonValue.Create(TimeSpan.Zero);
        }

        if (type == typeof(Uri))
        {
            return JsonValue.Create(string.Empty);
        }

        return null;
    }

    /// <summary>Null for anything that is not a collection, so an object is not mistaken for a list.</summary>
    private static Type? ElementTypeOf(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        // A dictionary is a JSON object, not an array, and sampling one as a list produces
        // something that will not deserialize at all.
        var enumerable = type
            .GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        var elementType = enumerable?.GetGenericArguments()[0];

        return elementType is { IsGenericType: true }
            && elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
                ? null
                : elementType;
    }

    /// <summary>Matches the serializer's naming policy, so the sample is what the endpoint expects.</summary>
    private static string CamelCase(string propertyName)
        => propertyName.Length == 0 ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
