using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Partners;

/// <summary>
/// Defines the Entity Framework mapping for partner.
/// </summary>
public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    /// <summary>
    /// Configures the database mapping for partner.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.HasHistory();
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.FromDate).IsRequired();
        builder.Property(p => p.Tier).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => new { p.Tier, p.FromDate });

        builder
            .HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(p => p.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasAuthors<Partner, PartnerId>();
    }
}
