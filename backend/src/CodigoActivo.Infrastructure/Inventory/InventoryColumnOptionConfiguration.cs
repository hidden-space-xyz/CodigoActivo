using CodigoActivo.Domain.Inventory;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Inventory;

/// <summary>
/// Defines the Entity Framework mapping for inventory column option.
/// </summary>
public sealed class InventoryColumnOptionConfiguration
    : IEntityTypeConfiguration<InventoryColumnOption>
{
    /// <summary>
    /// Configures the database mapping for inventory column option.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<InventoryColumnOption> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
    }
}
