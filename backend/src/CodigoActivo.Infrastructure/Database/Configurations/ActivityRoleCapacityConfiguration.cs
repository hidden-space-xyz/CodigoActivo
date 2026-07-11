using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class ActivityRoleCapacityConfiguration : IEntityTypeConfiguration<ActivityRoleCapacity>
{
    public void Configure(EntityTypeBuilder<ActivityRoleCapacity> builder)
    {
        builder.HasKey(x => new { x.ActivityId, x.ActivityRoleTypeId });

        builder.Property(x => x.DesiredCount).IsRequired();

        builder
            .HasOne(x => x.Activity)
            .WithMany(a => a.RoleCapacities)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.ActivityRoleType)
            .WithMany()
            .HasForeignKey(x => x.ActivityRoleTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
