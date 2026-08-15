using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public sealed class TermsDocumentConfiguration : IEntityTypeConfiguration<TermsDocument>
{
    public void Configure(EntityTypeBuilder<TermsDocument> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
