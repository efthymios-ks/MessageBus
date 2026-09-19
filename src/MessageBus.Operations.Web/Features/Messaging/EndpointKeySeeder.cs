using MessageBus.Operations.Storage;

namespace MessageBus.Operations.Web.Features.Messaging;

/// <summary>
/// Registration is the API key, and a registration is what makes silence alertable. Seeded from
/// configuration here so the sample runs; a deployment issues keys through the UI.
/// </summary>
public static class EndpointKeySeeder
{
    /// <summary>Inserts registrations for any key in the <c>Messaging:Operations:EndpointKeys</c> section that is missing from the table.</summary>
    public static async Task SeedEndpointKeysAsync(this WebApplication app)
    {
        var keys = app.Configuration.GetSection("Messaging:Operations:EndpointKeys").Get<Dictionary<string, string>>();
        if (keys is not { Count: > 0 })
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();

        foreach (var (endpointName, apiKey) in keys)
        {
            // "OVERRIDE" is the sentinel the appsettings catalogue uses to document the shape of
            // the section without seeding a real endpoint — Aspire (or a deployment) supplies the
            // actual value at run time.
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "OVERRIDE")
            {
                continue;
            }

            if (await dbContext
                .Endpoints
                .FindAsync(endpointName) is not null)
            {
                continue;
            }

            await dbContext
                .Endpoints
                .AddAsync(new()
                {
                    EndpointName = endpointName,
                    ApiKey = apiKey,
                    RegisteredAt = DateTimeOffset.UtcNow
                });
        }

        await dbContext.SaveChangesAsync();
    }
}
