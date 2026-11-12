using CodigoActivo.Domain.Inventory;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Inventory;

/// <summary>
/// Defines the Entity Framework mapping for inventory column.
/// </summary>
public sealed class InventoryColumnConfiguration : IEntityTypeConfiguration<InventoryColumn>
{
    /// <summary>
    /// Configures the database mapping for inventory column.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<InventoryColumn> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder
            .HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(option => option.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
