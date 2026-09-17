using System.Globalization;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Formats authenticator shared secrets for manual entry and enrollment URIs.
/// </summary>
public static class AuthenticatorKeys
{
    private const int GroupSize = 4;

    /// <summary>
    /// Splits the secret into groups of four characters separated by spaces.
    /// </summary>
    /// <param name="secret">Base32 shared secret.</param>
    /// <returns>The secret formatted for reading and typing.</returns>
    public static string Format(string secret)
    {
        return string.Join(
            ' ',
            Enumerable
                .Range(0, (secret.Length + GroupSize - 1) / GroupSize)
                .Select(index =>
                    secret.Substring(index * GroupSize, Math.Min(GroupSize, secret.Length - index * GroupSize))
                )
        );
    }

    /// <summary>
    /// Builds the <c>otpauth://totp</c> URI that authenticator applications read from a QR code.
    /// </summary>
    /// <param name="issuer">Name shown next to the account in the application.</param>
    /// <param name="account">Account label, normally the user's email address.</param>
    /// <param name="secret">Base32 shared secret.</param>
    /// <returns>The enrollment URI.</returns>
    public static string BuildUri(string issuer, string account, string secret)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(account);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30"
        );
    }
}
