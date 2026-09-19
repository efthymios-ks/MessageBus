namespace MessageBus.Abstractions.Handling;

/// <summary>
/// The name a saga's rows are stored under, which is what makes a rename or a move safe.
/// Without this attribute the stored name is the class's <see cref="Type.FullName"/>, so any
/// rename or namespace change strands every existing row.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SagaNameAttribute : Attribute
{
    /// <summary>Declares the persisted name of a saga.</summary>
    /// <param name="name">Stable name written to the <c>SagaTypeName</c> column.</param>
    public SagaNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>Name written to the <c>SagaTypeName</c> column.</summary>
    public string Name { get; }
}
