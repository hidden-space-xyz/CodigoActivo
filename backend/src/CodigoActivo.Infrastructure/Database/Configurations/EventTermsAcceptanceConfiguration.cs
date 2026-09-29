using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for event terms acceptance.
/// </summary>
public sealed class EventTermsAcceptanceConfiguration
    : IEntityTypeConfiguration<EventTermsAcceptance>
{
    /// <summary>
    /// Configures the database mapping for event terms acceptance.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EventTermsAcceptance> builder)
    {
        builder.HasKey(x => new
        {
            x.EventId,
            x.UserId,
            x.TermsDocumentId,
        });

        builder
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<TermsDocument>()
            .WithMany()
            .HasForeignKey(x => x.TermsDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.TermsDocumentId).IsRequired();
        builder.Property(x => x.Accepted).IsRequired();
        builder.Property(x => x.DecidedAt).IsRequired();
    }
}
