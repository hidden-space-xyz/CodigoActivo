using CodigoActivo.Domain.Inventory;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Inventory;

/// <summary>
/// Defines the Entity Framework mapping for inventory entry.
/// </summary>
public sealed class InventoryEntryConfiguration : IEntityTypeConfiguration<InventoryEntry>
{
    /// <summary>
    /// Configures the database mapping for inventory entry.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<InventoryEntry> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Values).IsRequired();
    }
}
