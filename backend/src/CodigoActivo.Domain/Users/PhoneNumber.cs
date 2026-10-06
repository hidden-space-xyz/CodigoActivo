using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Contact phone: digits with optional spaces, dots, hyphens and parentheses, and an optional plus
/// sign before the first digit, between seven and fifteen digits in total (the E.164 maximum). It
/// keeps the phone as typed, without the surrounding spaces.
/// </summary>
public sealed record PhoneNumber
{
    /// <summary>
    /// Fewest digits a phone may have.
    /// </summary>
    public const int MinDigits = 7;

    /// <summary>
    /// Most digits a phone may have.
    /// </summary>
    public const int MaxDigits = 15;

    private PhoneNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the trimmed phone.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Trims and checks a typed phone.
    /// </summary>
    /// <param name="value">Phone as typed, not blank.</param>
    /// <returns>The phone, or <see cref="DomainErrorCode.UserPhoneInvalid"/> when it has another shape.</returns>
    public static Result<PhoneNumber> Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();
        return HasValidShape(trimmed)
            ? new PhoneNumber(trimmed)
            : Error.Validation(DomainErrorCode.UserPhoneInvalid);
    }

    /// <summary>
    /// Restores a phone that was checked before it was stored.
    /// </summary>
    /// <param name="value">Stored phone.</param>
    /// <returns>The phone.</returns>
    public static PhoneNumber FromStored(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PhoneNumber(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    private static bool HasValidShape(string phone)
    {
        var digits = 0;
        var plus = false;
        foreach (var character in phone)
        {
            if (char.IsAsciiDigit(character))
            {
                digits++;
            }
            else if (character is '+' && !plus && digits is 0)
            {
                plus = true;
            }
            else if (character is not (' ' or '-' or '.' or '(' or ')'))
            {
                return false;
            }
        }

        return digits is >= MinDigits and <= MaxDigits;
    }
}
