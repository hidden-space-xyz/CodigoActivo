using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.Location).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder
            .HasOne(a => a.Event)
            .WithMany(e => e.Activities)
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(a => a.ActivityModalityType)
            .WithMany(t => t.Activities)
            .HasForeignKey(a => a.ActivityModalityTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(a => a.Thumbnail)
            .WithMany()
            .HasForeignKey(a => a.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
