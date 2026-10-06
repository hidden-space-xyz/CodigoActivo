using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Provides reusable extension methods for string.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Hides most of the local part of an email address so it can be shown to someone who has
    /// not yet proven ownership of the account, e.g. <c>a***@example.org</c>.
    /// </summary>
    /// <param name="email">Address to mask.</param>
    /// <returns>The masked address, or <see langword="null"/> when there is no address.</returns>
    public static string? MaskEmail(this string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
        {
            return "***";
        }

        return $"{email[0]}***{email[at..]}";
    }

    /// <summary>
    /// Hides most of the local part of an account email, as <see cref="MaskEmail(string?)"/> does.
    /// </summary>
    /// <param name="email">Address to mask.</param>
    /// <returns>The masked address, or <see langword="null"/> when there is no address.</returns>
    public static string? MaskEmail(this EmailAddress? email)
    {
        return email?.Value.MaskEmail();
    }
}
