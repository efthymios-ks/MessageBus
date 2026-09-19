using System.Text;
using System.Text.Json;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using MessageBus.Operations.Storage;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Actions;

/// <summary>
/// Retry, discard and correction. Republishing is Operations' own send: it holds the body and the
/// headers, so a retry works while the endpoint that failed the message is still down — which is
/// usually exactly when somebody wants it.
/// </summary>
public sealed class FailureActionService(
    OperationsDbContext dbContext,
    TransportSenderProvider senderProvider,
    TimeProvider timeProvider
)
{
    /// <summary>
    /// Republishes byte-for-byte to the endpoint that failed it. Retrying twice is harmless — the
    /// endpoint's inbox drops the second copy — so nothing here pretends otherwise by refusing.
    /// </summary>
    public Task<int> RetryAsync(IReadOnlyList<string> messageIds, string actor, CancellationToken cancellationToken)
        => DispatchAsync(messageIds, actor, MessageActionKind.Retry, destination: null, reason: null, cancellationToken);

    /// <summary>For a message that failed because it was routed wrong rather than because it was wrong.</summary>
    public Task<int> ReturnToSourceAsync(
        IReadOnlyList<string> messageIds,
        string destination,
        string actor,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        return DispatchAsync(
            messageIds,
            actor,
            MessageActionKind.ReturnToSource,
            destination,
            reason: null,
            cancellationToken
        );
    }

    /// <summary>
    /// Physically removes the failure rows after writing a <see cref="MessageActionKind.Delete"/>
    /// entry to the Actions log. The action row is what survives — questions like "what did we
    /// delete last quarter" still get an answer.
    /// </summary>
    public async Task<int> DeleteAsync(
        IReadOnlyList<string> messageIds,
        string actor,
        string reason,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var failures = await LoadAsync(messageIds, cancellationToken);

        if (failures.Count == 0)
        {
            return 0;
        }

        // Record first, remove second — the action row is what proves the delete happened, and
        // committing them together means a rolled-back delete never leaves a bogus audit trail.
        foreach (var failure in failures)
        {
            await RecordActionAsync(failure.MessageId, MessageActionKind.Delete, actor, reason, destination: null, cancellationToken);
        }

        dbContext
            .Failures
            .RemoveRange(failures);

        await dbContext.SaveChangesAsync(cancellationToken);

        return failures.Count;
    }

    /// <summary>
    /// Marks failures as written off without republishing. Kept as history because "what did we
    /// discard last quarter" is a real question.
    /// </summary>
    public async Task<int> DiscardAsync(
        IReadOnlyList<string> messageIds,
        string actor,
        string reason,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var failures = await LoadAsync(messageIds, cancellationToken);

        foreach (var failure in failures)
        {
            // Marked, never deleted. "What did we write off last quarter" is a real question and a
            // removed row cannot answer it.
            failure.Status = FailureStatus.Discarded;
            failure.ResolvedBy = actor;
            failure.ResolutionReason = reason;
            failure.ResolvedAt = timeProvider.GetUtcNow();

            await RecordActionAsync(failure.MessageId, MessageActionKind.Discard, actor, reason, destination: null, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return failures.Count;
    }

    /// <summary>
    /// Fabricates a corrected message carrying the original's provenance. Not an edit: the original
    /// is kept exactly as it failed, and what is dispatched is a new message that says where it came
    /// from.
    /// </summary>
    public async Task EditAndRetryAsync(
        string messageId,
        string correctedBody,
        IReadOnlyDictionary<string, string> correctedHeaders,
        string actor,
        string reason,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!IsValidJson(correctedBody))
        {
            throw new ArgumentException(
                "The corrected body must parse as JSON. A malformed edit turns a business failure "
                    + "into a deserialization failure, which cannot even be retried.",
                nameof(correctedBody)
            );
        }

        var failure = await dbContext
            .Failures
            .FindAsync([messageId], cancellationToken)
            ?? throw new InvalidOperationException($"No failure is stored for message '{messageId}'.");

        var headers = HeadersOf(failure);

        foreach (var header in correctedHeaders)
        {
            headers[header.Key] = header.Value;
        }

        var correctedMessageId = Guid.NewGuid().ToString();

        // Provenance rather than identity: a new id so deduplication treats it as a new message,
        // the same correlation so it lands in the same flow, and a pointer back to what it replaced.
        headers[MessageHeaders.MessageId] = correctedMessageId;
        headers[EditedFromHeader] = failure.MessageId;
        headers[EditedByHeader] = actor;
        headers[EditReasonHeader] = reason;

        headers.Remove(MessageHeaders.ExceptionType);
        headers.Remove(MessageHeaders.ExceptionMessage);
        headers.Remove(MessageHeaders.StackTrace);
        headers.Remove(MessageHeaders.FailedAt);
        headers.Remove(MessageHeaders.OriginalMessageId);

        var sender = await senderProvider.GetAsync(cancellationToken);

        await sender.TransmitAsync(
            [
                new TransportMessage
                {
                    MessageId = correctedMessageId,
                    MessageTypeName = failure.MessageTypeName,
                    Payload = Encoding.UTF8.GetBytes(correctedBody),
                    Headers = headers,
                    Destination = failure.EndpointName
                }
            ],
            cancellationToken
        );

        failure.Status = FailureStatus.Retried;
        failure.ResolvedBy = actor;
        failure.ResolutionReason = reason;
        failure.ResolvedAt = timeProvider.GetUtcNow();
        failure.EditedFromMessageId = correctedMessageId;

        await RecordActionAsync(failure.MessageId, MessageActionKind.EditAndRetry, actor, reason, failure.EndpointName, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Header naming the message id that this corrected copy replaces.</summary>
    public const string EditedFromHeader = "edited-from";

    /// <summary>Header naming the operator who applied the correction.</summary>
    public const string EditedByHeader = "edited-by";

    /// <summary>Header carrying the reason the operator gave for the correction.</summary>
    public const string EditReasonHeader = "edit-reason";

    /// <summary>True when <paramref name="body"/> parses as JSON. Used to guard corrected payloads before dispatch.</summary>
    public static bool IsValidJson(string body)
    {
        try
        {
            using var _ = JsonDocument.Parse(body);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<int> DispatchAsync(
        IReadOnlyList<string> messageIds,
        string actor,
        MessageActionKind kind,
        string? destination,
        string? reason,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var failures = await LoadAsync(messageIds, cancellationToken);

        if (failures.Count == 0)
        {
            return 0;
        }

        var sender = await senderProvider.GetAsync(cancellationToken);

        // One transmit for the batch. Two hundred failures of one kind is one action, and one round
        // trip rather than two hundred.
        await sender.TransmitAsync(
            [.. failures.Select(failure => ToTransportMessage(failure, destination))],
            cancellationToken
        );

        foreach (var failure in failures)
        {
            failure.Status = FailureStatus.Retried;
            failure.ResolvedBy = actor;
            failure.ResolutionReason = reason;
            failure.ResolvedAt = timeProvider.GetUtcNow();

            await RecordActionAsync(failure.MessageId, kind, actor, reason, destination ?? failure.EndpointName, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return failures.Count;
    }

    private async Task<IReadOnlyList<FailedMessage>> LoadAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        return await dbContext
            .Failures
            .Where(failure => messageIds.Contains(failure.MessageId))
            .ToArrayAsync(cancellationToken);
    }

    private async Task RecordActionAsync(
        string messageId,
        MessageActionKind kind,
        string actor,
        string? reason,
        string? destination,
        CancellationToken cancellationToken
    )
    {
        await dbContext
            .Actions
            .AddAsync(new()
            {
                MessageId = messageId,
                Kind = kind,
                Actor = actor,
                Reason = reason,
                Destination = destination,
                PerformedAt = timeProvider.GetUtcNow()
            }, cancellationToken);
    }

    private static TransportMessage ToTransportMessage(FailedMessage failure, string? destination)
    {
        var headers = HeadersOf(failure);

        // The original id goes back on, so the endpoint's inbox recognises a second retry as the
        // duplicate it is rather than handling the same work twice.
        headers[MessageHeaders.MessageId] = failure.MessageId;

        headers.Remove(MessageHeaders.ExceptionType);
        headers.Remove(MessageHeaders.ExceptionMessage);
        headers.Remove(MessageHeaders.StackTrace);
        headers.Remove(MessageHeaders.FailedAt);
        headers.Remove(MessageHeaders.OriginalMessageId);

        return new TransportMessage
        {
            MessageId = failure.MessageId,
            MessageTypeName = failure.MessageTypeName,
            Payload = failure.Payload,
            Headers = headers,
            Destination = destination ?? failure.EndpointName
        };
    }

    private static Dictionary<string, string> HeadersOf(FailedMessage failure)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(failure.Headers)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
