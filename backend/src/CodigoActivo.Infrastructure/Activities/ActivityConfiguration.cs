using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Defines the Entity Framework mapping for activity.
/// </summary>
public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    /// <summary>
    /// Configures the database mapping for activity.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.Location).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder
            .Property(a => a.Modality)
            .HasConversion(new CatalogConverter<ActivityModality>(CatalogIds.ActivityModalities))
            .HasColumnName("activity_modality_type_id");

        builder.Ignore(a => a.Schedule);
        builder.Property<DateTimeOffset>(ScheduleColumns.ActivityStartsAt);
        builder.Property<DateTimeOffset>(ScheduleColumns.ActivityEndsAt);

        builder.HasIndex(ScheduleColumns.ActivityStartsAt);
        builder.HasIndex(nameof(Activity.EventId), ScheduleColumns.ActivityStartsAt);

        builder
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ActivityModalityEntry>()
            .WithMany()
            .HasForeignKey(a => a.Modality)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<StoredFile>()
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
