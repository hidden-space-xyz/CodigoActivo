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
    /// Name of the shadow property that maps the PostgreSQL <c>xmin</c> system column, which
    /// changes with every write of the row. It is the concurrency token of a user: saving or
    /// deleting a user whose row another transaction wrote since it was read affects no row and
    /// fails instead of mixing both writes. It adds no column to the table.
    /// </summary>
    public const string RowVersion = nameof(RowVersion);

    /// <summary>
    /// Configures the database mapping for user.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property<uint>(RowVersion).IsRowVersion().HasColumnName("xmin");

        builder.Property(u => u.FirstName).IsRequired();
        builder.Property(u => u.LastName).IsRequired();
        builder.Property(u => u.NationalId).HasMaxLength(9);
        builder.Property(u => u.NewsConsent).IsRequired();
        builder.Property(u => u.Gender).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder
            .Property(u => u.TwoFactorMethod)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.ComplexProperty(
            u => u.EmailChange,
            change =>
            {
                change.Property(c => c.NewEmail).HasColumnName("pending_email");
                change.Property(c => c.CodeHash).HasColumnName("email_change_code_hash");
                change.Property(c => c.ExpiresAt).HasColumnName("email_change_expires_at");
            }
        );
        builder.ComplexProperty(
            u => u.VerificationCode,
            code => MapOneTimeCode(code, "otp_code_hash", "otp_expires_at", "otp_last_sent_at")
        );
        builder.ComplexProperty(
            u => u.PasswordResetCode,
            code =>
                MapOneTimeCode(
                    code,
                    "password_reset_code_hash",
                    "password_reset_expires_at",
                    "password_reset_last_sent_at"
                )
        );
        builder.ComplexProperty(
            u => u.LoginCode,
            code =>
                MapOneTimeCode(
                    code,
                    "login_code_hash",
                    "login_code_expires_at",
                    "login_code_last_sent_at"
                )
        );
        builder.ComplexProperty(
            u => u.Authenticator,
            authenticator =>
            {
                authenticator.Property(a => a.Key).HasColumnName("authenticator_key");
                authenticator
                    .Property(a => a.LastUsedStep)
                    .HasColumnName("authenticator_last_used_step");
                authenticator
                    .Property(a => a.PendingKey)
                    .HasColumnName("pending_authenticator_key");
                authenticator
                    .Property(a => a.PendingExpiresAt)
                    .HasColumnName("pending_authenticator_expires_at");
            }
        );
        builder.ComplexProperty(
            u => u.LoginChallenge,
            challenge =>
            {
                challenge.Property(c => c.Id).HasColumnName("login_challenge_id");
                challenge.Property(c => c.StartedAt).HasColumnName("login_challenge_started_at");
            }
        );
        builder.ComplexProperty(
            u => u.TwoFactorLockout,
            lockout =>
            {
                lockout.Property(l => l.FailedAttempts).HasColumnName("two_factor_failed_attempts");
                lockout.Property(l => l.LockedUntil).HasColumnName("two_factor_locked_until");
            }
        );
        builder.ComplexProperty(
            u => u.PasswordLockout,
            lockout =>
            {
                lockout.Property(l => l.FailedAttempts).HasColumnName("password_failed_attempts");
                lockout.Property(l => l.LockedAt).HasColumnName("password_locked_at");
            }
        );

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

    private static void MapOneTimeCode(
        ComplexPropertyBuilder<OneTimeCode> code,
        string hashColumn,
        string expiresAtColumn,
        string lastSentAtColumn
    )
    {
        code.Property(c => c.Hash).HasColumnName(hashColumn);
        code.Property(c => c.ExpiresAt).HasColumnName(expiresAtColumn);
        code.Property(c => c.LastSentAt).HasColumnName(lastSentAtColumn);
    }
}
