using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Defines the Entity Framework mapping for assignment.
/// </summary>
public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    /// <summary>
    /// Configures the database mapping for assignment.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.HasKey(x => new
        {
            x.UserId,
            x.ActivityId,
            x.Role,
        });

        builder
            .Property(x => x.Role)
            .HasConversion(new CatalogConverter<ActivityRole>(CatalogIds.ActivityRoles))
            .HasColumnName("activity_role_type_id");
        builder
            .Property(x => x.Status)
            .HasConversion(new CatalogConverter<AssignmentStatus>(CatalogIds.AssignmentStatuses))
            .HasColumnName("assignment_status_id");

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
            .HasOne<ActivityRoleEntry>()
            .WithMany()
            .HasForeignKey(x => x.Role)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<AssignmentStatusEntry>()
            .WithMany()
            .HasForeignKey(x => x.Status)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
