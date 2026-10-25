using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Defines the Entity Framework mapping for stored file.
/// </summary>
public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    /// <summary>
    /// Configures the database mapping for stored file.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.HasHistory();
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
