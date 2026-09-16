using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for announcement.
/// </summary>
public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    /// <summary>
    /// Configures the database mapping for announcement.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired();
        builder.Property(a => a.Subtitle).IsRequired();
        builder.Property(a => a.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.Featured).HasDefaultValue(false);

        builder.HasIndex(a => a.CreatedAt);

        builder
            .HasOne(a => a.Thumbnail)
            .WithMany()
            .HasForeignKey(a => a.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
