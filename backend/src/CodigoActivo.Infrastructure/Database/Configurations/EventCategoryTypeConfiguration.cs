using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for event category type.
/// </summary>
public sealed class EventCategoryTypeConfiguration : IEntityTypeConfiguration<EventCategoryType>
{
    /// <summary>
    /// Configures the database mapping for event category type.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EventCategoryType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
