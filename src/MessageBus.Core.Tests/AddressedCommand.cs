using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

/// <summary>Carries its destination, for the attribute router.</summary>
[MessageDestination("somewhere-service")]
public sealed class AddressedCommand : ICommand;
