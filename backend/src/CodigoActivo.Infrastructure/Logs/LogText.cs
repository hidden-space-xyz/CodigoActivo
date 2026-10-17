using System.Text;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Turns any text a log event carries into text PostgreSQL accepts: it drops NUL characters,
/// replaces unpaired surrogates, which are not valid UTF-8, and truncates without splitting a
/// surrogate pair.
/// </summary>
public static class LogText
{
    private const char Replacement = '�';

    /// <summary>
    /// Cleans and truncates a text.
    /// </summary>
    /// <param name="value">Text to store.</param>
    /// <param name="maxLength">Maximum length, in UTF-16 code units, of the result.</param>
    /// <returns>The storable text.</returns>
    public static string Clean(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (value.Length <= maxLength && !NeedsCleaning(value))
        {
            return value;
        }

        var cleaned = new StringBuilder(Math.Min(value.Length, maxLength));
        for (var i = 0; i < value.Length && cleaned.Length < maxLength; i++)
        {
            var current = value[i];
            if (current is '\0')
            {
                continue;
            }

            if (!char.IsSurrogate(current))
            {
                cleaned.Append(current);
            }
            else if (
                char.IsHighSurrogate(current)
                && i + 1 < value.Length
                && char.IsLowSurrogate(value[i + 1])
            )
            {
                if (cleaned.Length + 2 > maxLength)
                {
                    break;
                }

                cleaned.Append(current).Append(value[++i]);
            }
            else
            {
                cleaned.Append(Replacement);
            }
        }

        return cleaned.ToString();
    }

    /// <summary>
    /// Cleans and truncates a text that may be missing.
    /// </summary>
    /// <param name="value">Text to store, if any.</param>
    /// <param name="maxLength">Maximum length, in UTF-16 code units, of the result.</param>
    /// <returns>The storable text, or <see langword="null"/> when there is none.</returns>
    public static string? CleanOrNull(string? value, int maxLength)
    {
        return value is null ? null : Clean(value, maxLength);
    }

    private static bool NeedsCleaning(string value)
    {
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            return true;
        }

        var rest = value.AsSpan();
        var index = rest.IndexOfAnyInRange('\uD800', '\uDFFF');
        while (index >= 0)
        {
            if (
                !char.IsHighSurrogate(rest[index])
                || index + 1 >= rest.Length
                || !char.IsLowSurrogate(rest[index + 1])
            )
            {
                return true;
            }

            rest = rest[(index + 2)..];
            index = rest.IndexOfAnyInRange('\uD800', '\uDFFF');
        }

        return false;
    }
}
