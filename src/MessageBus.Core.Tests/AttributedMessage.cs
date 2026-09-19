using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Tests;

/// <summary>Carries its wire name, for the attributed type map.</summary>
[MessageName("Tests.Attributed.v1")]
public sealed class AttributedMessage : IEvent;
