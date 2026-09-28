namespace CodigoActivo.Domain.Common;

/// <summary>
/// Normalizes free text to the form it is stored and looked up in, so values typed with different
/// spacing or casing still match.
/// </summary>
public static class TextNormalization
{
    /// <summary>
    /// Trims the value, treating blank text as missing.
    /// </summary>
    /// <param name="value">Value to normalize.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when it is blank.</returns>
    public static string? NormalizeOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Trims and lowercases an email address, treating blank text as missing. Stored addresses and
    /// login lookups use this same form.
    /// </summary>
    /// <param name="value">Address to normalize.</param>
    /// <returns>The normalized address, or <see langword="null"/> when it is blank.</returns>
    public static string? NormalizeEmailOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
