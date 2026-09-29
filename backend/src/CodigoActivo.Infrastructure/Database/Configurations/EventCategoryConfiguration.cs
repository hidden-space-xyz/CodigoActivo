using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for event category.
/// </summary>
public sealed class EventCategoryConfiguration : IEntityTypeConfiguration<EventCategory>
{
    /// <summary>
    /// Configures the database mapping for event category.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EventCategory> builder)
    {
        builder.HasKey(x => new { x.EventId, x.EventCategoryTypeId });

        builder
            .HasOne<Event>()
            .WithMany(e => e.Categories)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<EventCategoryType>()
            .WithMany()
            .HasForeignKey(x => x.EventCategoryTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
