using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public sealed class AssignmentStatusTypeConfiguration
    : IEntityTypeConfiguration<AssignmentStatusType>
{
    public void Configure(EntityTypeBuilder<AssignmentStatusType> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}