using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for email outbox content.
/// </summary>
public sealed class EmailOutboxContentConfiguration : IEntityTypeConfiguration<EmailOutboxContent>
{
    /// <summary>
    /// Configures the database mapping for email outbox content.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EmailOutboxContent> builder)
    {
        builder.HasKey(content => content.Id);

        builder.Property(content => content.Subject).IsRequired();
        builder.Property(content => content.HtmlBody).IsRequired();
        builder.Property(content => content.TextBody).IsRequired();
        builder.Property(content => content.CreatedAt).IsRequired();

        builder
            .HasMany(content => content.Parts)
            .WithOne(part => part.Content)
            .HasForeignKey(part => part.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
