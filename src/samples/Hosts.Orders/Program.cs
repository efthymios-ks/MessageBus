using MessageBus.Explorer;
using MessageBus.Hosts.Orders.Features.Persistence;
using MessageBus.Hosts.Orders.Features.Orders;
using MessageBus.Hosts.Orders.Features.Messaging;
using MessageBus.Hosts.ServiceDefaults;
using MessageBus.Core.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddOrdersPersistence();
builder.AddOrdersMessaging();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOrderEndpoints();
app.MapMessageExplorer(configuration => configuration.GetValue("Messaging:Explorer:Enabled", false));

await app.MigrateOrdersAsync();
await app.UseMessagingAsync();

await app.RunAsync();
