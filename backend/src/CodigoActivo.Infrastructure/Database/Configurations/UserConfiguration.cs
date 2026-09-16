using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for user.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Configures the database mapping for user.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName).IsRequired();
        builder.Property(u => u.LastName).IsRequired();
        builder.Property(u => u.BirthDate).IsRequired();
        builder.Property(u => u.Gender).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Phone).IsUnique();
        builder.HasIndex(u => new { u.FirstName, u.LastName });

        builder
            .HasOne(u => u.UserStatusType)
            .WithMany(s => s.Users)
            .HasForeignKey(u => u.UserStatusTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(u => u.UserType)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.UserTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(u => u.Parent)
            .WithMany(u => u.Children)
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
