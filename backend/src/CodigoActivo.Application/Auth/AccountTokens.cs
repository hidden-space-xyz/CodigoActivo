using System.Security.Cryptography;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Creates the single-use secrets that travel in emailed account links.
/// </summary>
public static class AccountTokens
{
    private const int TokenBytes = 32;

    /// <summary>
    /// Generates a cryptographically random single-use token with 256 bits of entropy, encoded as
    /// lowercase hexadecimal so it is safe inside a URL fragment and matches the lowercase form
    /// <see cref="OtpValidator"/> verifies.
    /// </summary>
    /// <returns>A 64-character lowercase hexadecimal token.</returns>
    public static string Create()
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));
    }
}
