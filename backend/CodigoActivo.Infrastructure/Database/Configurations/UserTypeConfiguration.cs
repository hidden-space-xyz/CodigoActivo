using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public sealed class UserTypeConfiguration : IEntityTypeConfiguration<UserType>
{
    public void Configure(EntityTypeBuilder<UserType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
        builder.Property(x => x.Hidden).IsRequired();
        builder.Property(x => x.IsAllowedForMinors).IsRequired();
        builder.Property(x => x.IsAllowedForAdults).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}