using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for user status type.
/// </summary>
public sealed class UserStatusTypeConfiguration : IEntityTypeConfiguration<UserStatusType>
{
    /// <summary>
    /// Configures the database mapping for user status type.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<UserStatusType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
