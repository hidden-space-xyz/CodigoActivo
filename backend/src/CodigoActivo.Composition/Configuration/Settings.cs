using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CodigoActivo.Composition.Configuration;

/// <summary>
/// Reads settings strictly: a missing or blank setting takes its default, and a setting that is
/// present but cannot be used stops the application instead of being silently replaced.
/// </summary>
/// <param name="configuration">Configuration the settings are read from.</param>
internal sealed class Settings(IConfiguration configuration)
{
    /// <summary>
    /// Reads a text setting, trimmed.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <param name="fallback">Value when the setting is missing or blank.</param>
    /// <returns>The value.</returns>
    public string Text(string key, string fallback)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>
    /// Reads a setting exactly as written, such as a password whose spaces matter.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <returns>The value, or an empty text when the setting is missing.</returns>
    public string Verbatim(string key)
    {
        return configuration[key] ?? string.Empty;
    }

    /// <summary>
    /// Reads a positive whole number.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <param name="fallback">Value when the setting is missing or blank.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The setting is not a positive whole number.</exception>
    public int PositiveInt(string key, int fallback)
    {
        return Read(
            key,
            fallback,
            value =>
                int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
                && parsed > 0
                    ? parsed
                    : null,
            "a positive whole number"
        );
    }

    /// <summary>
    /// Reads a member of an enumeration by name, ignoring case.
    /// </summary>
    /// <typeparam name="TEnum">Enumeration read.</typeparam>
    /// <param name="key">Configuration key.</param>
    /// <param name="fallback">Value when the setting is missing or blank.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The setting names no member.</exception>
    public TEnum Choice<TEnum>(string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Read(
            key,
            fallback,
            value =>
                Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed)
                    ? parsed
                    : (TEnum?)null,
            $"one of {string.Join(", ", Enum.GetNames<TEnum>())}"
        );
    }

    /// <summary>
    /// Reads a time zone by its IANA identifier. Windows identifiers are refused everywhere, since the
    /// API image runs without ICU and cannot convert them.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <param name="fallback">Value when the setting is missing or blank.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The setting names no known IANA time zone.</exception>
    public TimeZoneInfo TimeZone(string key, TimeZoneInfo fallback)
    {
        return Read(key, fallback, FindIanaTimeZone, "a known IANA time zone");
    }

    private T Read<T>(string key, T fallback, Func<string, T?> parse, string expected)
        where T : class
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return parse(value.Trim()) ?? throw Invalid(key, expected);
    }

    private T Read<T>(string key, T fallback, Func<string, T?> parse, string expected)
        where T : struct
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return parse(value.Trim()) ?? throw Invalid(key, expected);
    }

    private static InvalidOperationException Invalid(string key, string expected)
    {
        return new InvalidOperationException($"The setting {key} must be {expected}.");
    }

    private static TimeZoneInfo? FindIanaTimeZone(string id)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId
            ? zone
            : null;
    }
}
