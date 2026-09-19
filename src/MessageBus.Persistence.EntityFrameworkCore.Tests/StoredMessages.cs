using MessageBus.Core.Persistence;
using System.Text.Json;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

internal static class StoredMessages
{
    public static StoredMessage Create(string destination = "some-endpoint", string? messageTypeName = null)
        => new(
            MessageId: Guid.NewGuid(),
            MessageTypeName: messageTypeName ?? "Tests.Message.v1",
            Destination: destination,
            Payload: Convert.ToBase64String("{}"u8.ToArray()),
            Headers: JsonSerializer.Serialize(new Dictionary<string, string>())
        );
}
