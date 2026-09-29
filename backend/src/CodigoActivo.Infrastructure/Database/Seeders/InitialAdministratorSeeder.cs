using System.Net.Mail;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Creates the initial administrator, under <see cref="SeedIds.Users.InitialAdministrator"/>, on
/// an empty database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="passwordHasher">Service used to securely hash and verify passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class InitialAdministratorSeeder(
    CodigoActivoDbContext context,
    IPasswordHasher passwordHasher,
    IClock clock
)
{
    /// <summary>
    /// Creates the initial administrator when the database has no users. A database that has users
    /// but not the initial administrator is refused, because deleting accounts relies on it.
    /// </summary>
    /// <param name="configuredEmail">The configured email value.</param>
    /// <param name="configuredPassword">The configured password value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The bootstrap credentials are invalid on an empty database, or the database has users but not the initial administrator.</exception>
    public async Task SeedAsync(
        string? configuredEmail,
        string? configuredPassword,
        CancellationToken ct = default
    )
    {
        if (await context.Users.AnyAsync(ct))
        {
            if (!await context.Users.AnyAsync(u => u.Id == SeedIds.Users.InitialAdministrator, ct))
            {
                throw new InvalidOperationException(
                    $"The database has users but not the initial administrator {SeedIds.Users.InitialAdministrator}; a database created before that account had a fixed identifier must be recreated."
                );
            }

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

        context.Users.Add(
            User.CreateInitialAdministrator(
                email,
                passwordHasher.Hash(configuredPassword),
                clock.UtcNow
            )
        );
        await context.SaveChangesAsync(ct);
    }
}
