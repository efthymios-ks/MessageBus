using System.Collections.Frozen;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Routing;

internal sealed class MessageRouteBuilder : IMessageRouteBuilder
{
    private readonly Dictionary<Type, string> _destinationsByType = [];

    public IMessageRouteBuilder Map<TCommand>(string destination)
        where TCommand : ICommand
        => Map(typeof(TCommand), destination);

    public IMessageRouteBuilder Map(Type commandType, string destination)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        _destinationsByType[commandType] = destination;

        return this;
    }

    public IMessageRouteBuilder MapAssemblyOf<TCommand>(string destination)
        where TCommand : ICommand
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var commandTypes = typeof(TCommand).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(typeof(ICommand).IsAssignableFrom);

        foreach (var commandType in commandTypes)
        {
            Map(commandType, destination);
        }

        return this;
    }

    public FrozenDictionary<Type, string> Build()
        => _destinationsByType.ToFrozenDictionary();
}
