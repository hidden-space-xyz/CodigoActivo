using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines the Entity Framework mapping for email outbox message, which keeps no data history. The
/// kind is stored by name, the rows are indexed by priority and schedule, the order of the claim, by
/// topic and address, the key a newer message replaces, and by expiry, and a content cannot be
/// deleted while a message references it.
/// </summary>
public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    /// <summary>
    /// Configures the database mapping for email outbox message.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Kind).HasConversion<string>().IsRequired();
        builder.Property(message => message.Priority).IsRequired();
        builder.Property(message => message.ToAddress).IsRequired();
        builder.Property(message => message.ToName).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.Property(message => message.AttemptCount).IsRequired();
        builder.Property(message => message.NextAttemptAt).IsRequired();
        builder
            .Property(message => message.LastError)
            .HasMaxLength(EmailOutboxMessage.LastErrorMaxLength);
        builder.Property(message => message.Topic).HasMaxLength(EmailTopics.MaxLength);

        builder.HasIndex(message => new { message.Priority, message.NextAttemptAt });
        builder.HasIndex(message => message.ContentId);
        builder
            .HasIndex(message => new { message.Topic, message.ToAddress })
            .HasFilter("topic IS NOT NULL");
        builder.HasIndex(message => message.ExpiresAt).HasFilter("expires_at IS NOT NULL");

        builder
            .HasOne(message => message.Content)
            .WithMany()
            .HasForeignKey(message => message.ContentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
