using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for email outbox message.
/// </summary>
public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    /// <summary>
    /// Configures the database mapping for email outbox message.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
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

        builder.HasIndex(message => new { message.Priority, message.NextAttemptAt });
        builder.HasIndex(message => message.ContentId);

        builder
            .HasOne(message => message.Content)
            .WithMany()
            .HasForeignKey(message => message.ContentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
