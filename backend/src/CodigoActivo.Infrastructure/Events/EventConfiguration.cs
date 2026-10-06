using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Events;

/// <summary>
/// Defines the Entity Framework mapping for event.
/// </summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <summary>
    /// Configures the database mapping for event.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Subtitle).IsRequired();
        builder.Property(e => e.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.Ignore(e => e.Calendar);
        builder.Ignore(e => e.SignupWindow);
        builder.Property<DateOnly>(ScheduleColumns.EventStartsAt);
        builder.Property<DateOnly>(ScheduleColumns.EventEndsAt);
        builder.Property<DateTimeOffset?>(ScheduleColumns.EarlySignupStartsAt);
        builder.Property<DateTimeOffset>(ScheduleColumns.SignupStartsAt);
        builder.Property<DateTimeOffset>(ScheduleColumns.SignupEndsAt);

        builder.HasIndex(ScheduleColumns.EventStartsAt);
        builder.HasIndex(ScheduleColumns.EventEndsAt);
        builder.HasIndex(e => e.Featured).IsUnique().HasFilter("featured");

        builder
            .HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(e => e.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
