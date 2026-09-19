namespace MessageBus.Transport.RabbitMq;

/// <summary>A single binding entry from the RabbitMQ management API's queue bindings response.</summary>
internal sealed record RabbitMqBindingResponse(string Source);
