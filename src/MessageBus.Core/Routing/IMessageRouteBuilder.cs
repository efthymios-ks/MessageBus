using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Routing;

/// <summary>
/// Where each command goes, declared at registration. Verbose on purpose: it is the one place a
/// reviewer sees the whole topology.
/// </summary>
public interface IMessageRouteBuilder
{
    /// <summary>Routes the command to the given destination.</summary>
    IMessageRouteBuilder Map<TCommand>(string destination)
        where TCommand : ICommand;

    /// <summary>Routes the given command type to the destination.</summary>
    IMessageRouteBuilder Map(Type commandType, string destination);

    /// <summary>Every command in the assembly, to one endpoint.</summary>
    IMessageRouteBuilder MapAssemblyOf<TCommand>(string destination)
        where TCommand : ICommand;
}
