using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for resource.
/// </summary>
public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    /// <summary>
    /// Configures the database mapping for resource.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired();
        builder.Property(r => r.Subtitle).IsRequired();
        builder.Property(r => r.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.CreatedAt);

        builder
            .HasOne(r => r.ResourceType)
            .WithMany(t => t.Resources)
            .HasForeignKey(r => r.ResourceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(r => r.Thumbnail)
            .WithMany()
            .HasForeignKey(r => r.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
