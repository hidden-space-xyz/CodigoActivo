using CodigoActivo.Domain.Users;
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
        builder.Property(u => u.NationalId).HasMaxLength(9);
        builder.Property(u => u.PromotionalConsent).IsRequired();
        builder.Property(u => u.Gender).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder
            .Property(u => u.TwoFactorMethod)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => new { u.FirstName, u.LastName });

        builder
            .HasOne<UserStatusType>()
            .WithMany()
            .HasForeignKey(u => u.UserStatusTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<UserType>()
            .WithMany()
            .HasForeignKey(u => u.UserTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
