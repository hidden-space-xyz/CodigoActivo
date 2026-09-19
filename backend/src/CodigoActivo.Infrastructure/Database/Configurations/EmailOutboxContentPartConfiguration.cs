using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for email outbox content part.
/// </summary>
public sealed class EmailOutboxContentPartConfiguration
    : IEntityTypeConfiguration<EmailOutboxContentPart>
{
    /// <summary>
    /// Configures the database mapping for email outbox content part.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EmailOutboxContentPart> builder)
    {
        builder.HasKey(part => part.Id);

        builder.Property(part => part.Disposition).HasConversion<string>().IsRequired();
        builder.Property(part => part.FileName).IsRequired();
        builder.Property(part => part.ContentType).IsRequired();
        builder.Property(part => part.Payload).IsRequired();
        builder.Property(part => part.DisplayOrder).IsRequired();

        builder.HasIndex(part => part.ContentId);
    }
}
