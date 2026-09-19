namespace MessageBus.Abstractions.Messages;

/// <summary>
/// The name this message travels under, which is what consumers bind to. Declaring it decouples
/// the wire from the CLR name, so a rename stays a local refactor.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageNameAttribute : Attribute
{
    /// <summary>Declares the wire name this message travels under.</summary>
    /// <param name="name">Wire name. Convention: <c>{Namespace}.{Type}.v{N}</c>.</param>
    public MessageNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>Wire name this message travels under.</summary>
    public string Name { get; }
}
