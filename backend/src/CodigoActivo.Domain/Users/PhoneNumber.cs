namespace CodigoActivo.Domain.Users;

/// <summary>
/// Shape of a contact phone: digits with optional spaces, dots, hyphens and parentheses, and an
/// optional plus sign before the first digit, between seven and fifteen digits in total (the
/// E.164 maximum).
/// </summary>
public static class PhoneNumber
{
    /// <summary>
    /// Fewest digits a phone may have.
    /// </summary>
    public const int MinDigits = 7;

    /// <summary>
    /// Most digits a phone may have.
    /// </summary>
    public const int MaxDigits = 15;

    /// <summary>
    /// Tells whether a trimmed phone has the accepted shape.
    /// </summary>
    /// <param name="phone">Phone to check, already trimmed.</param>
    /// <returns><see langword="true"/> when the phone is well formed.</returns>
    public static bool IsValid(string phone)
    {
        ArgumentNullException.ThrowIfNull(phone);

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
