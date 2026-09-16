using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for terms document.
/// </summary>
public sealed class TermsDocumentConfiguration : IEntityTypeConfiguration<TermsDocument>
{
    /// <summary>
    /// Configures the database mapping for terms document.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<TermsDocument> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
