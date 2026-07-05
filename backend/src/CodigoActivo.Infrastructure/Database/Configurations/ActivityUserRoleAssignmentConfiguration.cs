using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class ActivityUserRoleAssignmentConfiguration
    : IEntityTypeConfiguration<ActivityUserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<ActivityUserRoleAssignment> builder)
    {
        builder.HasKey(x => new
        {
            x.UserId,
            x.ActivityId,
            x.ActivityRoleTypeId,
        });

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Activity)
            .WithMany(a => a.Assignments)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.ActivityRoleType)
            .WithMany(t => t.Assignments)
            .HasForeignKey(x => x.ActivityRoleTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.AssignmentStatus)
            .WithMany(s => s.Assignments)
            .HasForeignKey(x => x.AssignmentStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}