using CodigoActivo.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Legal;

/// <summary>
/// Defines the Entity Framework mapping for the versions a legal policy replaced; the version in
/// force lives in the policy itself.
/// </summary>
public sealed class LegalPolicyVersionConfiguration : IEntityTypeConfiguration<LegalPolicyVersion>
{
    /// <summary>
    /// Configures the database mapping for the versions of the legal policies.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<LegalPolicyVersion> builder)
    {
        builder.HasKey(x => new { x.LegalPolicyId, x.Number });
        builder.Property(x => x.Number).ValueGeneratedNever();
        builder.Property(x => x.Content).IsRequired();

        builder
            .HasOne<LegalPolicy>()
            .WithMany(x => x.PreviousVersions)
            .HasForeignKey(x => x.LegalPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
