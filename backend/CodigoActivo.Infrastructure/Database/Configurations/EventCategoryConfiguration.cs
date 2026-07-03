using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public sealed class EventCategoryConfiguration : IEntityTypeConfiguration<EventCategory>
{
    public void Configure(EntityTypeBuilder<EventCategory> builder)
    {
        builder.HasKey(x => new { x.EventId, x.EventCategoryTypeId });

        builder
            .HasOne(x => x.Event)
            .WithMany(e => e.Categories)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.EventCategoryType)
            .WithMany(t => t.Events)
            .HasForeignKey(x => x.EventCategoryTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}