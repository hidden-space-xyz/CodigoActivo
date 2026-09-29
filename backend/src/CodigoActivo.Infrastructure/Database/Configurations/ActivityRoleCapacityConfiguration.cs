using CodigoActivo.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for activity role capacity.
/// </summary>
public class ActivityRoleCapacityConfiguration : IEntityTypeConfiguration<ActivityRoleCapacity>
{
    /// <summary>
    /// Configures the database mapping for activity role capacity.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<ActivityRoleCapacity> builder)
    {
        builder.HasKey(x => new { x.ActivityId, x.ActivityRoleTypeId });

        builder.Property(x => x.DesiredCount).IsRequired();

        builder
            .HasOne<Activity>()
            .WithMany(a => a.RoleCapacities)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ActivityRoleType>()
            .WithMany()
            .HasForeignKey(x => x.ActivityRoleTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
