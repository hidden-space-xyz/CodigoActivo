using System.Security.Cryptography;
using System.Text;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Derives the credential stamp carried by session and second-factor cookies. It changes whenever
/// the password hash changes, so a password change or reset invalidates every issued cookie.
/// </summary>
public static class CredentialStamps
{
    /// <summary>
    /// Computes the stamp of the supplied password hash.
    /// </summary>
    /// <param name="passwordHash">Stored password hash of the account.</param>
    /// <returns>The hexadecimal SHA-256 digest of the hash.</returns>
    public static string For(string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
    }

    /// <summary>
    /// Compares a presented stamp with the expected one in constant time.
    /// </summary>
    /// <param name="presented">Stamp read from the cookie.</param>
    /// <param name="expected">Stamp computed from the current password hash.</param>
    /// <returns><see langword="true"/> when both stamps are equal.</returns>
    public static bool Matches(string presented, string expected)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(expected);
        return presented.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(presented),
                Encoding.ASCII.GetBytes(expected)
            );
    }
}
