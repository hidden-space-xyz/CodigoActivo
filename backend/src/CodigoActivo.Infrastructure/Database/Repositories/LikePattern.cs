namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Builds SQL LIKE patterns that match a value literally, so names containing wildcards compare as
/// typed.
/// </summary>
internal static class LikePattern
{
    /// <summary>
    /// Escape character used by the patterns this class builds.
    /// </summary>
    public const string EscapeCharacter = "\\";

    /// <summary>
    /// Escapes the LIKE wildcards of a value so it only matches itself.
    /// </summary>
    /// <param name="value">Value to match literally.</param>
    /// <returns>The pattern that matches exactly the value.</returns>
    public static string Literal(string value)
    {
        return value
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
    }
}
