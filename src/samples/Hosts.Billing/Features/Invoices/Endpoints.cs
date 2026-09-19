using Microsoft.EntityFrameworkCore;
using MessageBus.Hosts.Billing.Features.Persistence;

namespace MessageBus.Hosts.Billing.Features.Invoices;

public static class Endpoints
{
    public static WebApplication MapInvoiceEndpoints(this WebApplication app)
    {
        app.MapGet("/invoices", async (BillingDbContext dbContext, CancellationToken cancellationToken)
            => await dbContext.Invoices.ToArrayAsync(cancellationToken));

        return app;
    }
}
