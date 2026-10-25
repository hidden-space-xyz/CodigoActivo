using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.TermsDocuments;

/// <summary>
/// Defines the Entity Framework mapping for the versions a terms document replaced. The decisions
/// name a version by number without a foreign key, since the current version lives in the
/// document itself.
/// </summary>
public sealed class TermsDocumentVersionConfiguration
    : IEntityTypeConfiguration<TermsDocumentVersion>
{
    /// <summary>
    /// Configures the database mapping for the versions of the terms documents.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<TermsDocumentVersion> builder)
    {
        builder.HasHistory();
        builder.HasKey(x => new { x.TermsDocumentId, x.Number });
        builder.Property(x => x.Number).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.RequiresSignature).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder
            .HasOne<TermsDocument>()
            .WithMany(x => x.PreviousVersions)
            .HasForeignKey(x => x.TermsDocumentId)
            .OnDelete(DeleteBehavior.ClientCascade);
    }
}
