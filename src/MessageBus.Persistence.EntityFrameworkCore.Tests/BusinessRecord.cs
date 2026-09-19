namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

/// <summary>Stands in for the application's own data, so a test can prove the two commit together.</summary>
public sealed class BusinessRecord
{
    public required string Id { get; set; }
}
