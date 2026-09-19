using MessageBus.Operations.Web.Features.Health;
using MessageBus.Operations.Web.Features.Messaging;
using MessageBus.Operations.Web.Features.OpenTelemetry;
using MessageBus.Operations.Web.Features.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddOperationsTelemetry();
builder.AddOperationsHealth();
builder.AddOperationsPersistence();
builder.AddOperationsMessaging();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();

app.MapOperationsHealth();
app.MapRazorPages();
app.MapHeartbeatsEndpoint();

await app.MigrateOperationsAsync();
await app.SeedEndpointKeysAsync();

await app.RunAsync();
