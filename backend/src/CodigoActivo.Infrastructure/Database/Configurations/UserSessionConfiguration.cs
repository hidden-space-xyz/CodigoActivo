using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for user session.
/// </summary>
public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    /// <summary>
    /// Configures the database mapping for user session.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();

        builder.HasIndex(s => s.UserId);

        builder
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
