namespace CodigoActivo.Application.Extensions;

public static class StringExtensions
{
    public static string? NormalizeOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Trims and lower-cases an email so storage, uniqueness checks and login lookups all use one
    /// canonical form — otherwise a case-variant registers as a duplicate account and login by a
    /// differently-cased address fails against PostgreSQL's case-sensitive text comparison.
    /// </summary>
    public static string? NormalizeEmailOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}