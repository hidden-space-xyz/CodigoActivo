using System.Net.Mail;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Database.Seeders;

public sealed class InitialAdministratorSeeder(
    CodigoActivoDbContext context,
    IPasswordHasher passwordHasher,
    IClock clock,
    ILogger<InitialAdministratorSeeder> logger
)
{
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
            UserTypeId = SeedIds.UserTypes.Participant,
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
