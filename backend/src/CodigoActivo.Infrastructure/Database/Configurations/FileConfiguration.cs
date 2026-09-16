using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for file.
/// </summary>
public class FileConfiguration : IEntityTypeConfiguration<FileEntity>
{
    /// <summary>
    /// Configures the database mapping for file.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).IsRequired();
        builder.Property(f => f.Extension).IsRequired();
        builder.Property(f => f.UploadedAt).IsRequired();

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
