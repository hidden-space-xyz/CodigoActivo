using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for disposable email domain.
/// </summary>
public sealed class DisposableEmailDomainConfiguration
    : IEntityTypeConfiguration<DisposableEmailDomain>
{
    /// <summary>
    /// Longest domain name DNS allows.
    /// </summary>
    public const int MaxDomainLength = 253;

    /// <summary>
    /// Configures the database mapping for disposable email domain.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<DisposableEmailDomain> builder)
    {
        builder.HasKey(d => d.Domain);

        builder.Property(d => d.Domain).HasMaxLength(MaxDomainLength);
    }
}
