using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Users;

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

        builder
            .Property(u => u.Status)
            .HasConversion(new CatalogConverter<UserStatus>(CatalogIds.UserStatuses))
            .HasColumnName("user_status_type_id");
        builder
            .Property(u => u.UserType)
            .HasConversion(new CatalogConverter<UserType>(CatalogIds.UserTypes))
            .HasColumnName("user_type_id");

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => new { u.FirstName, u.LastName });

        builder
            .HasOne<UserStatusEntry>()
            .WithMany()
            .HasForeignKey(u => u.Status)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<UserTypeEntry>()
            .WithMany()
            .HasForeignKey(u => u.UserType)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
