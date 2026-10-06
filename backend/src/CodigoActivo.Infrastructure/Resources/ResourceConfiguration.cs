using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Resources;

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

        builder
            .Property(r => r.ResourceType)
            .HasConversion(new CatalogConverter<ResourceType>(CatalogIds.ResourceTypes))
            .HasColumnName("resource_type_id");

        builder.HasIndex(r => r.CreatedAt);

        builder
            .HasOne<ResourceTypeEntry>()
            .WithMany()
            .HasForeignKey(r => r.ResourceType)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<StoredFile>()
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
