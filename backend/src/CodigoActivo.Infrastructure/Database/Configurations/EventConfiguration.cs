using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for event.
/// </summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <summary>
    /// Configures the database mapping for event.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Subtitle).IsRequired();
        builder.Property(e => e.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Featured).HasDefaultValue(false);

        builder.HasIndex(e => e.EventStartsAt);
        builder.HasIndex(e => e.EventEndsAt);

        builder
            .HasOne(e => e.Thumbnail)
            .WithMany()
            .HasForeignKey(e => e.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.TermsDocument)
            .WithMany()
            .HasForeignKey(e => e.TermsDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
