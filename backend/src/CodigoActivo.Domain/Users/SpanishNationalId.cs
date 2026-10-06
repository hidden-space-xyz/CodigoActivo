using System.Globalization;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Spanish national identity number: a DNI (eight digits and a control letter) or a NIE (X, Y or
/// Z, seven digits and a control letter). The number, reading a NIE's X, Y or Z as 0, 1 or 2,
/// modulo 23 picks the control letter, so a mistyped digit changes the expected letter. The stored
/// form is uppercase, without spaces or hyphens.
/// </summary>
public sealed record SpanishNationalId
{
    private const string ControlLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const int MaxDniNumber = 99_999_999;

    private SpanishNationalId(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the normalized DNI or NIE.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Normalizes and checks a typed DNI or NIE.
    /// </summary>
    /// <param name="value">Value as typed.</param>
    /// <returns>
    /// The identity number, <see cref="DomainErrorCode.UserNationalIdRequired"/> when nothing was
    /// typed, or <see cref="DomainErrorCode.UserNationalIdInvalid"/> when its control letter does not
    /// match its number.
    /// </returns>
    public static Result<SpanishNationalId> Create(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return Error.Validation(DomainErrorCode.UserNationalIdRequired);
        }

        return IsValid(normalized)
            ? new SpanishNationalId(normalized)
            : Error.Validation(DomainErrorCode.UserNationalIdInvalid);
    }

    /// <summary>
    /// Restores an identity number that was checked before it was stored.
    /// </summary>
    /// <param name="value">Stored identity number.</param>
    /// <returns>The identity number.</returns>
    public static SpanishNationalId FromStored(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new SpanishNationalId(value);
    }

    /// <summary>
    /// Converts a typed DNI or NIE to its stored form: uppercase, without spaces or hyphens. It does
    /// not check the value, so filters can search by a partial one.
    /// </summary>
    /// <param name="value">Value as typed.</param>
    /// <returns>The normalized value, or <see langword="null"/> when nothing remains.</returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Concat(
            value.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant)
        );
        return normalized.Length is 0 ? null : normalized;
    }

    /// <summary>
    /// Determines whether the value, once normalized, is a DNI or NIE whose control letter matches
    /// its number.
    /// </summary>
    /// <param name="value">Value as typed or stored.</param>
    /// <returns><see langword="true"/> for a well-formed DNI or NIE; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(string? value)
    {
        if (Normalize(value) is not { Length: 9 } normalized)
        {
            return false;
        }

        var leading = normalized[0] switch
        {
            'X' => '0',
            'Y' => '1',
            'Z' => '2',
            var other => other,
        };
        var digits = leading + normalized[1..8];
        if (!digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var number = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        return normalized[8] == ControlLetter(number);
    }

    /// <summary>
    /// Builds the DNI of a number, padding it to eight digits and appending its control letter.
    /// </summary>
    /// <param name="number">DNI number, between 0 and 99,999,999.</param>
    /// <returns>The DNI.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number does not fit in eight digits.</exception>
    public static SpanishNationalId FromDniNumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxDniNumber);
        return new SpanishNationalId(
            string.Create(CultureInfo.InvariantCulture, $"{number:D8}{ControlLetter(number)}")
        );
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    private static char ControlLetter(int number)
    {
        return ControlLetters[number % ControlLetters.Length];
    }
}
