using CodigoActivo.Domain.TermsDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.TermsDocuments;

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
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.RequiresSignature).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
