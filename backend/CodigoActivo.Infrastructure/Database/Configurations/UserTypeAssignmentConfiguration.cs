using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class UserTypeAssignmentConfiguration : IEntityTypeConfiguration<UserTypeAssignment>
{
    public void Configure(EntityTypeBuilder<UserTypeAssignment> builder)
    {
        builder.HasKey(x => new { x.UserId, x.UserTypeId });

        builder.Property(x => x.AssignedAt).IsRequired();

        builder
            .HasOne(x => x.User)
            .WithMany(u => u.TypeAssignments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.UserType)
            .WithMany(t => t.Assignments)
            .HasForeignKey(x => x.UserTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}