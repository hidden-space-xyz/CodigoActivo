using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for activity role type.
/// </summary>
public sealed class ActivityRoleTypeConfiguration : IEntityTypeConfiguration<ActivityRoleType>
{
    /// <summary>
    /// Configures the database mapping for activity role type.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<ActivityRoleType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
