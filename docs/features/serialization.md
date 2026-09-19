# Serialization

JSON default.
MessagePack option.
Custom hook.

```csharp
.WithJsonSerializer()
.WithJsonSerializer(json => json.Converters.Add(new MoneyJsonConverter()))
.WithMessagePackSerializer()
.WithSerializer<TSerializer>()
```

## JSON defaults

- Case-insensitive reads.
- Enums as strings.
- Nulls omitted when writing.
- Numbers accepted from strings.
- Unknown members ignored.

Each keeps an additive contract change from breaking a consumer.

## MessagePack

Second package: `MessageBus.Serialization.MessagePack`.
Contractless — the contracts package stays free of MessagePack attributes.
Compresses payloads.
Refuses untrusted sizes by default.

Trade-off:

- Smaller and faster.
- No longer readable in a broker's management UI. Costs you when staring at a stuck queue.

Both ends must agree.
`content-type` header travels along so a consumer sees what it was given.
Nothing translates.

## Custom

Implement `IMessageSerializer` and register with `.WithSerializer<TSerializer>()`.
`ContentType` on the implementation lands in the header.
Consumers use it to reject or dispatch to a peer serializer.
