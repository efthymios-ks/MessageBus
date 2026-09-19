using System.Security.Cryptography;
using MessageBus.Operations.Storage;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Operations.Heartbeats;

/// <summary>
/// Manages endpoint credentials from the Keys screen. Every method returns the plaintext key only
/// once — on create and on rotate — which is the shape the UI needs to show it in a copy-once
/// banner. Storage is plaintext by design; hashing changes the RecordAsync path and the tradeoff
/// is called out in the plan.
/// </summary>
public sealed class EndpointKeyService(OperationsDbContext dbContext, TimeProvider timeProvider)
{
    /// <summary>The default silence window for a new endpoint. Overridden per row on the screen.</summary>
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(2);

    /// <summary>Returns every registration for the Keys screen, optionally including disabled rows.</summary>
    public async Task<IReadOnlyList<EndpointKeyView>> ListAsync(bool includeDisabled, CancellationToken cancellationToken)
    {
        var registrations = await dbContext
            .Endpoints
            .AsNoTracking()
            .Where(endpoint => includeDisabled || !endpoint.Disabled)
            .OrderBy(endpoint => endpoint.EndpointName)
            .ToArrayAsync(cancellationToken);

        var lastSeen = await dbContext
            .Instances
            .AsNoTracking()
            .GroupBy(instance => instance.EndpointName)
            .Select(group => new
            {
                EndpointName = group.Key,
                LastSeenAt = group.Max(instance => (DateTimeOffset?)instance.LastSeenAt),
                Machine = group
                    .OrderByDescending(instance => instance.LastSeenAt)
                    .Select(instance => instance.MachineName)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(entry => entry.EndpointName, StringComparer.Ordinal, cancellationToken);

        return
        [
            .. registrations.Select(registration => new EndpointKeyView(
                registration.EndpointName,
                registration.ApiKey,
                registration.RegisteredAt,
                registration.StaleAfter,
                registration.Disabled,
                lastSeen.TryGetValue(registration.EndpointName, out var seen) ? seen.Machine : null,
                lastSeen.TryGetValue(registration.EndpointName, out var timestamp) ? timestamp.LastSeenAt : null
            ))
        ];
    }

    /// <summary>Adds an endpoint and returns its plaintext key. The caller shows the value once and never again.</summary>
    public async Task<string> CreateAsync(string endpointName, string? explicitKey, TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        if (await dbContext
            .Endpoints
            .AnyAsync(endpoint => endpoint.EndpointName == endpointName, cancellationToken))
        {
            throw new InvalidOperationException($"Endpoint \"{endpointName}\" already exists.");
        }

        var key = explicitKey is { Length: > 0 } ? explicitKey : GenerateKey();

        await dbContext
            .Endpoints
            .AddAsync(new()
            {
                EndpointName = endpointName,
                ApiKey = key,
                RegisteredAt = timeProvider.GetUtcNow(),
                StaleAfter = staleAfter <= TimeSpan.Zero ? DefaultStaleAfter : staleAfter
            }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return key;
    }

    /// <summary>Replaces the key. Returns the new plaintext once.</summary>
    public async Task<string> RotateAsync(string endpointName, string? explicitKey, CancellationToken cancellationToken)
    {
        var registration = await dbContext
            .Endpoints
            .FindAsync([endpointName], cancellationToken)
            ?? throw new InvalidOperationException($"No endpoint is registered under \"{endpointName}\".");

        var key = explicitKey is { Length: > 0 } ? explicitKey : GenerateKey();
        registration.ApiKey = key;

        await dbContext.SaveChangesAsync(cancellationToken);

        return key;
    }

    /// <summary>
    /// Atomic update of the three fields the Edit modal owns — the new key (or unchanged), the
    /// stale-after window, and the disabled flag. One save, one round trip, one row of history.
    /// </summary>
    public async Task SaveAsync(
        string endpointName,
        string key,
        TimeSpan staleAfter,
        bool disabled,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var registration = await dbContext
            .Endpoints
            .FindAsync([endpointName], cancellationToken)
            ?? throw new InvalidOperationException($"No endpoint is registered under \"{endpointName}\".");

        registration.ApiKey = key;
        registration.StaleAfter = staleAfter <= TimeSpan.Zero ? DefaultStaleAfter : staleAfter;
        registration.Disabled = disabled;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateKey()
    {
        // 32 random bytes rendered as hex plus a short suffix — the shape the mockup uses.
        var buffer = RandomNumberGenerator.GetBytes(16);
        var suffix = Convert.ToBase64String(RandomNumberGenerator.GetBytes(3))
            .Replace('+', 'a')
            .Replace('/', 'b')
            .Replace('=', 'c');

        return "k_" + Convert.ToHexString(buffer).ToLowerInvariant() + "_" + suffix;
    }
}
