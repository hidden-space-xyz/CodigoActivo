namespace CodigoActivo.Application.Extensions;

/// <summary>
/// Provides reusable extension methods for string.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Normalizes the supplied or null value.
    /// </summary>
    /// <param name="value">Value to validate or convert.</param>
    /// <returns>The resulting string value.</returns>
    public static string? NormalizeOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Normalizes the supplied email or null value.
    /// </summary>
    /// <param name="value">Value to validate or convert.</param>
    /// <returns>The resulting string value.</returns>
    public static string? NormalizeEmailOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
