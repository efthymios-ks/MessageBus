using MessageBus.Persistence.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore.Modeling;

/// <summary>Applies the messaging model to a <see cref="ModelBuilder"/>.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds the outbox, inbox, delayed and saga tables to a context's model. Called explicitly from
    /// <see cref="DbContext.OnModelCreating"/> rather than injected through an <see cref="Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer"/>: a table that
    /// appears in a migration without a line of code to explain it is a table nobody owns. One
    /// <c>dotnet ef migrations add</c> then covers all four.
    /// </summary>
    public static ModelBuilder ApplyMessagingModel(
        this ModelBuilder modelBuilder,
        Action<MessagingModelOptions>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var options = new MessagingModelOptions();
        configure?.Invoke(options);

        modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration(options));
        modelBuilder.ApplyConfiguration(new InboxMessageEntityConfiguration(options));
        modelBuilder.ApplyConfiguration(new DelayedMessageEntityConfiguration(options));
        modelBuilder.ApplyConfiguration(new SagaEntityConfiguration(options));

        return modelBuilder;
    }
}
