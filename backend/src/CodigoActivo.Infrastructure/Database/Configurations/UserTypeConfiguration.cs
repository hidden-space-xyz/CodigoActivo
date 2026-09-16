using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for user type.
/// </summary>
public sealed class UserTypeConfiguration : IEntityTypeConfiguration<UserType>
{
    /// <summary>
    /// Configures the database mapping for user type.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<UserType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
