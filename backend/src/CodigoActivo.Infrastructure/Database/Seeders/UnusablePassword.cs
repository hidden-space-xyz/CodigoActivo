using System.Security.Cryptography;
using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Gives seeded accounts a password nobody knows: it is generated on each call and never stored,
/// logged or shown anywhere, so the account only gets a usable password through a password reset.
/// </summary>
internal static class UnusablePassword
{
    /// <summary>
    /// Bytes of entropy in the discarded password.
    /// </summary>
    private const int Bytes = 32;

    /// <summary>
    /// Hashes a new random password and discards it.
    /// </summary>
    /// <param name="passwordHasher">Service used to securely hash and verify passwords.</param>
    /// <returns>The hash of a password nobody knows.</returns>
    public static string Hash(IPasswordHasher passwordHasher)
    {
        return passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(Bytes)));
    }
}
