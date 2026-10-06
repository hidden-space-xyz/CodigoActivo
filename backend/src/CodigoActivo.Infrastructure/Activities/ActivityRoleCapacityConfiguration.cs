using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Activities;

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
        builder.HasKey(x => new { x.ActivityId, x.Role });

        builder
            .Property(x => x.Role)
            .HasConversion(new CatalogConverter<ActivityRole>(CatalogIds.ActivityRoles))
            .HasColumnName("activity_role_type_id");

        builder.Property(x => x.DesiredCount).IsRequired();

        builder
            .HasOne<Activity>()
            .WithMany(a => a.RoleCapacities)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ActivityRoleEntry>()
            .WithMany()
            .HasForeignKey(x => x.Role)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
