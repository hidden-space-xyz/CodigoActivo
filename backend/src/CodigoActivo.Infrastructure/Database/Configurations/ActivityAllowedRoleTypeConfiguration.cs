using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class ActivityAllowedRoleTypeConfiguration
    : IEntityTypeConfiguration<ActivityAllowedRoleType>
{
    public void Configure(EntityTypeBuilder<ActivityAllowedRoleType> builder)
    {
        builder.HasKey(x => new { x.ActivityId, x.ActivityRoleTypeId });

        builder
            .HasOne(x => x.Activity)
            .WithMany(a => a.AllowedRoleTypes)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.ActivityRoleType)
            .WithMany(t => t.AllowedInActivities)
            .HasForeignKey(x => x.ActivityRoleTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
