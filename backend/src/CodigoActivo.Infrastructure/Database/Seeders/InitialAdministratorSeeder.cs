using System.Net.Mail;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Creates the initial initial administrator records when they are missing.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="passwordHasher">Service used to securely hash and verify passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class InitialAdministratorSeeder(
    CodigoActivoDbContext context,
    IPasswordHasher passwordHasher,
    IClock clock,
    ILogger<InitialAdministratorSeeder> logger
)
{
    /// <summary>
    /// Creates the required initial administrator records when they do not exist.
    /// </summary>
    /// <param name="configuredEmail">The configured email value.</param>
    /// <param name="configuredPassword">The configured password value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SeedAsync(
        string? configuredEmail,
        string? configuredPassword,
        CancellationToken ct = default
    )
    {
        if (await context.Users.AnyAsync(ct))
        {
            return;
        }

        var email = configuredEmail?.Trim().ToLowerInvariant();
        if (
            string.IsNullOrEmpty(email)
            || !MailAddress.TryCreate(email, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "BOOTSTRAP_ADMIN_EMAIL must be a single valid email address when the database has no users."
            );
        }

        if (
            string.IsNullOrWhiteSpace(configuredPassword)
            || configuredPassword.Length is < 12 or > 128
        )
        {
            throw new InvalidOperationException(
                "BOOTSTRAP_ADMIN_PASSWORD must contain between 12 and 128 characters when the database has no users."
            );
        }

        var administrator = new User
        {
            FirstName = "Administrador",
            LastName = "Código Activo",
            Email = email,
            Phone = null,
            PasswordHash = passwordHasher.Hash(configuredPassword),
            BirthDate = new DateOnly(2000, 1, 1),
            Gender = Gender.Other,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            IsAdmin = true,
            CreatedAt = clock.UtcNow,
        };
        context.Users.Add(administrator);
        await context.SaveChangesAsync(ct);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Created the initial administrator account {UserId}",
                administrator.Id
            );
        }
    }
}
