using MessageBus.Explorer;
using MessageBus.Hosts.ServiceDefaults;
using MessageBus.Hosts.Billing.Features.Invoices;
using MessageBus.Hosts.Billing.Features.Persistence;
using MessageBus.Hosts.Billing.Features.Messaging;
using MessageBus.Core.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBillingPersistence();
builder.AddBillingMessaging();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapInvoiceEndpoints();
app.MapMessageExplorer(configuration => configuration.GetValue("Messaging:Explorer:Enabled", false));

await app.MigrateBillingAsync();
await app.UseMessagingAsync();
await app.RunAsync();
