using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for event terms document.
/// </summary>
public sealed class EventTermsDocumentConfiguration : IEntityTypeConfiguration<EventTermsDocument>
{
    /// <summary>
    /// Configures the database mapping for event terms document.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EventTermsDocument> builder)
    {
        builder.HasKey(x => new { x.EventId, x.TermsDocumentId });

        builder
            .HasOne(x => x.Event)
            .WithMany(e => e.TermsDocuments)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TermsDocument)
            .WithMany()
            .HasForeignKey(x => x.TermsDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.DisplayOrder).IsRequired();
    }
}
