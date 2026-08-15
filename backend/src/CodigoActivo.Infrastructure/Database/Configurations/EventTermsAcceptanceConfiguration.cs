using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public sealed class EventTermsAcceptanceConfiguration
    : IEntityTypeConfiguration<EventTermsAcceptance>
{
    public void Configure(EntityTypeBuilder<EventTermsAcceptance> builder)
    {
        builder.HasKey(x => new { x.EventId, x.UserId });

        builder
            .HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<TermsDocument>()
            .WithMany()
            .HasForeignKey(x => x.TermsDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.TermsDocumentId).IsRequired();
        builder.Property(x => x.AcceptedAt).IsRequired();
    }
}
