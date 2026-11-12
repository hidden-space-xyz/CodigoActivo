using CodigoActivo.Domain.Inventory;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Inventory;

/// <summary>
/// Defines the Entity Framework mapping for inventory category.
/// </summary>
public sealed class InventoryCategoryConfiguration : IEntityTypeConfiguration<InventoryCategory>
{
    /// <summary>
    /// Configures the database mapping for inventory category.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<InventoryCategory> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().UseCollation(CaseInsensitiveNames.Collation);

        builder
            .HasMany(x => x.Columns)
            .WithOne()
            .HasForeignKey(column => column.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Entries)
            .WithOne()
            .HasForeignKey(entry => entry.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
