using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public sealed class ActivityRoleTypeConfiguration : IEntityTypeConfiguration<ActivityRoleType>
{
    public void Configure(EntityTypeBuilder<ActivityRoleType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
