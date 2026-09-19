using MessageBus.Abstractions.Messages;

namespace MessageBus.Explorer.Tests;

/// <summary>A contract that points at itself, which is the shape that would loop forever.</summary>
public sealed class Recursive : ICommand
{
    public string Name { get; set; } = string.Empty;

    public Recursive? Child { get; set; }
}
