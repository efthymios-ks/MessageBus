using System.Text.Json;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Handling;

/// <summary>
/// Saga state as stored. Deliberately not the message serializer: state is this endpoint's private
/// storage, never a wire contract, so it needs none of the cross-version tolerance a message does.
/// </summary>
internal static class SagaStateSerializer
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(object state)
        => JsonSerializer.Serialize(state, state.GetType(), _serializerOptions);

    public static SagaState Deserialize(string state, Type stateType)
        => (SagaState)JsonSerializer.Deserialize(state, stateType, _serializerOptions)!;
}
