using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.TermsDecisions;

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
        builder.HasHistory();
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
            .OnDelete(DeleteBehavior.ClientCascade);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.ClientCascade);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.DecidedById)
            .OnDelete(DeleteBehavior.ClientCascade);

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
