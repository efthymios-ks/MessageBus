using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Hosts.Orders.Contracts;

namespace MessageBus.Hosts.Orders.Features.Messaging;

/// <summary>
/// Consumes the reply from Billing after Orders asked <see cref="LookupInvoice"/>.
/// A reply is a normal command — this endpoint has to have a handler for it or the message
/// lands in the error queue as <c>NoHandlerForMessageException</c>.
/// </summary>
public sealed class InvoiceLookupResponseHandler(ILogger<InvoiceLookupResponseHandler> logger)
    : IMessageHandler<InvoiceLookupResponse>
{
    public Task HandleAsync(InvoiceLookupResponse message, IMessageContext messageContext)
    {
        logger.LogInformation(
            "Billing replied for {OrderId}. Invoice: {InvoiceId}",
            message.OrderId,
            message.InvoiceId ?? "(none)");

        return Task.CompletedTask;
    }
}
