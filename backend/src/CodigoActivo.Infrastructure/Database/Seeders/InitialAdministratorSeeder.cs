using System.Net.Mail;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Creates the initial administrator, under <see cref="InitialAdministrator.Id"/>, on
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
    /// Creates the initial administrator when the database has no users, with a password nobody
    /// knows: its owner chooses one through a password reset sent to the configured email. A
    /// database that has users but not the initial administrator is refused, because deleting
    /// accounts relies on it.
    /// </summary>
    /// <param name="configuredEmail">The configured email value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">The bootstrap email is invalid on an empty database, or the database has users but not the initial administrator.</exception>
    public async Task SeedAsync(string? configuredEmail, CancellationToken ct = default)
    {
        if (await context.Users.AnyAsync(ct))
        {
            if (!await context.Users.AnyAsync(u => u.Id == InitialAdministrator.Id, ct))
            {
                throw new InvalidOperationException(
                    $"The database has users but not the initial administrator {InitialAdministrator.Id}; a database created before that account had a fixed identifier must be recreated."
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

        context.Users.Add(
            User.CreateInitialAdministrator(
                EmailAddress.Create(email).Value,
                UnusablePassword.Hash(passwordHasher),
                clock.UtcNow
            )
        );
        await context.SaveChangesAsync(ct);
    }
}
