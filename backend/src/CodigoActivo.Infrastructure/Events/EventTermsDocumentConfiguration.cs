using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Events;

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
            .HasOne<Event>()
            .WithMany(e => e.TermsDocuments)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<TermsDocument>()
            .WithMany()
            .HasForeignKey(x => x.TermsDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.DisplayOrder).IsRequired();
    }
}
