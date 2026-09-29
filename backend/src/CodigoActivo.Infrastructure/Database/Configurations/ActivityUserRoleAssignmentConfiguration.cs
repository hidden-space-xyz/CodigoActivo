using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for activity user role assignment.
/// </summary>
public class ActivityUserRoleAssignmentConfiguration
    : IEntityTypeConfiguration<ActivityUserRoleAssignment>
{
    /// <summary>
    /// Configures the database mapping for activity user role assignment.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<ActivityUserRoleAssignment> builder)
    {
        builder.HasKey(x => new
        {
            x.UserId,
            x.ActivityId,
            x.ActivityRoleTypeId,
        });

        builder.HasIndex(x => new { x.UserId, x.ActivityId }).IsUnique();

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Activity>()
            .WithMany(a => a.Assignments)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ActivityRoleType>()
            .WithMany()
            .HasForeignKey(x => x.ActivityRoleTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<AssignmentStatusType>()
            .WithMany()
            .HasForeignKey(x => x.AssignmentStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
