using CodigoActivo.Domain.Legal;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Legal;

/// <summary>
/// Defines the Entity Framework mapping for the legal policies, one row per kind.
/// </summary>
public sealed class LegalPolicyConfiguration : IEntityTypeConfiguration<LegalPolicy>
{
    /// <summary>
    /// Configures the database mapping for the legal policies.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<LegalPolicy> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.HasIndex(x => x.Kind).IsUnique();
    }
}
